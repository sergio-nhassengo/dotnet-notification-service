using System.Net.Http.Json;
using System.Net;
using System.Text.Json.Nodes;
using Application.Common.Interfaces;
using Application.Features.Notifications.Commands.CreateEmail;
using Confluent.Kafka;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Notifications.Kafka;
using Infrastructure.Notifications.Options;
using Infrastructure.Notifications.Workers;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Persistence;

namespace Api.IntegrationTests;

public sealed class NotificationReliabilityTests
{
    [Theory]
    [InlineData(NotificationStatus.Pending)]
    [InlineData(NotificationStatus.Queued)]
    [InlineData(NotificationStatus.RetryScheduled)]
    [InlineData(NotificationStatus.Sent)]
    [InlineData(NotificationStatus.PermanentlyFailed)]
    [InlineData(NotificationStatus.DeadLettered)]
    public async Task Accepted_events_are_idempotent_and_commit_after_save(NotificationStatus status)
    {
        var interceptor = new SaveProbe();
        await using var factory = new NotificationSqliteFactory(interceptor: interceptor);
        var item = await Submit(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var n = await db.EmailNotifications.SingleAsync();
        SetState(n, status);
        await db.SaveChangesAsync();
        var saves = interceptor.Saves;
        var consumer = Substitute.For<IConsumer<string, string>>();
        consumer.When(c => c.Commit(item)).Do(_ => Assert.True(interceptor.Saves > saves));
        using var worker = Worker(factory, consumer);
        await worker.ProcessAndCommitAsync(item, default);
        await worker.ProcessAndCommitAsync(item, default);
        consumer.Received(2).Commit(item);
        db.ChangeTracker.Clear();
        Assert.Equal(status == NotificationStatus.Pending ? NotificationStatus.Queued : status,
            (await db.EmailNotifications.SingleAsync()).Status);
        Assert.Equal(1, await db.OutboxMessages.CountAsync());
        Assert.Equal(saves + 2, interceptor.Saves);
    }

    [Fact]
    public async Task Failed_persistence_does_not_commit_and_retry_accepts_same_record()
    {
        var interceptor = new SaveProbe();
        await using var factory = new NotificationSqliteFactory(interceptor: interceptor);
        var item = await Submit(factory);
        var consumer = Substitute.For<IConsumer<string, string>>();
        using var worker = Worker(factory, consumer);
        interceptor.FailNext = true;
        await Assert.ThrowsAsync<DbUpdateException>(() => worker.ProcessAndCommitAsync(item, default));
        consumer.DidNotReceive().Commit(Arg.Any<ConsumeResult<string, string>>());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(NotificationStatus.Pending, (await db.EmailNotifications.AsNoTracking().SingleAsync()).Status);
        await worker.ProcessAndCommitAsync(item, default);
        consumer.Received(1).Commit(item);
        Assert.Equal(NotificationStatus.Queued, (await db.EmailNotifications.AsNoTracking().SingleAsync()).Status);
    }

    [Theory]
    [InlineData("applicationId")]
    [InlineData("idempotencyKey")]
    [InlineData("requestHash")]
    [InlineData("content")]
    [InlineData("unknown")]
    public async Task Invalid_events_go_to_dlq_without_changing_rest_record(string field)
    {
        var interceptor = new SaveProbe();
        await using var factory = new NotificationSqliteFactory(interceptor: interceptor);
        var item = await Submit(factory);
        var json = JsonNode.Parse(item.Message.Value)!;
        if (field == "unknown")
        {
            var id = Guid.NewGuid();
            json["messageId"] = id;
            item.Message.Key = id.ToString();
        }
        else if (field == "content") json["content"]!["textBody"] = "tampered";
        else json[field] = "mismatch";
        item.Message.Value = json.ToJsonString();
        var consumer = Substitute.For<IConsumer<string, string>>();
        using var worker = Worker(factory, consumer);
        interceptor.FailNext = true;
        await Assert.ThrowsAsync<DbUpdateException>(() => worker.ProcessAndCommitAsync(item, default));
        consumer.DidNotReceive().Commit(Arg.Any<ConsumeResult<string, string>>());
        await worker.ProcessAndCommitAsync(item, default);
        await worker.ProcessAndCommitAsync(item, default); // Invalid events are at least once.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(NotificationStatus.Pending, (await db.EmailNotifications.SingleAsync()).Status);
        Assert.Equal(2, await db.OutboxMessages.CountAsync(x => x.Topic == "dlq-topic-notification"));
        consumer.Received(2).Commit(item);
    }

    [Fact]
    public async Task Cleanup_deletes_only_expired_attachments_across_applications()
    {
        await using var factory = new NotificationSqliteFactory();
        _ = factory.Client("otc-api");
        var now = DateTimeOffset.UtcNow;
        var clock = Substitute.For<IDateTime>(); clock.Now.Returns(now);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        foreach (var app in new[] { "otc-api", "tan-webservice" })
        foreach (var created in new[] { now.AddDays(-8), now.AddDays(-7), now })
            db.NotificationAttachments.Add(new NotificationAttachment
            {
                Id = Guid.NewGuid(), ApplicationId = app, FileName = "a.txt", ContentType = "text/plain",
                Content = [1], CreatedAt = created
            });
        await db.SaveChangesAsync();
        using var worker = new AttachmentCleanupWorker(factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new NotificationAttachmentOptions { RetentionDays = 7 }), clock,
            NullLogger<AttachmentCleanupWorker>.Instance);
        Assert.Equal(2, await worker.CleanupAsync(default));
        Assert.Equal(0, await worker.CleanupAsync(default));
        Assert.Equal(4, await db.NotificationAttachments.CountAsync());
    }

    private static KafkaEmailConsumerWorker Worker(NotificationSqliteFactory factory, IConsumer<string, string> consumer) =>
        new(consumer, factory.Services.GetRequiredService<KafkaEmailMessageProcessor>(),
            Options.Create(new KafkaOptions()), NullLogger<KafkaEmailConsumerWorker>.Instance);

    private static async Task<ConsumeResult<string, string>> Submit(NotificationSqliteFactory factory)
    {
        var response = await factory.Client("otc-api").PostAsJsonAsync("/api/v1/notifications/email",
            new CreateEmailNotificationCommand(Guid.NewGuid().ToString(), "c", null, null, null, "Subject")
            { To = [new("a@example.com")], TextBody = "body" });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var outbox = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.SingleAsync();
        return new ConsumeResult<string, string>
        {
            Topic = outbox.Topic, Partition = new Partition(0), Offset = new Offset(42),
            Message = new Message<string, string> { Key = outbox.MessageKey.ToString(), Value = outbox.Payload }
        };
    }

    private static void SetState(EmailNotification n, NotificationStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        switch (status)
        {
            case NotificationStatus.Queued: n.MarkQueued(now); break;
            case NotificationStatus.RetryScheduled: n.ScheduleRetry("retry", "retry", now.AddMinutes(1), now); break;
            case NotificationStatus.Sent: n.MarkSent(now); break;
            case NotificationStatus.PermanentlyFailed: n.MarkPermanentlyFailed("failed", "failed", now); break;
            case NotificationStatus.DeadLettered: n.MarkDeadLettered(now); break;
        }
    }

    private sealed class SaveProbe : SaveChangesInterceptor
    {
        public bool FailNext { get; set; }
        public int Saves { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (FailNext)
            {
                FailNext = false;
                throw new DbUpdateException("Simulated persistence failure");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            Saves++;
            return new(result);
        }
    }
}
