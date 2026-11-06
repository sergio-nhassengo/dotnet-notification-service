using System.Text.Json;
using Application.Common.Retry;
using Application.Features.Notifications.Commands.DeliverEmail;
using Application.Notifications.Abstractions;
using Application.Notifications.Contracts;
using Application.Notifications.Models;
using Application.UnitTests.Common;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using NSubstitute.ExceptionExtensions;
using NSubstitute;
using Persistence;

namespace Application.UnitTests.Notifications;

public sealed class DeliverEmailCommandHandlerTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly IEmailProvider _provider = Substitute.For<IEmailProvider>();
    private readonly IEmailTemplateRenderer _renderer = Substitute.For<IEmailTemplateRenderer>();
    private readonly INotificationDefaults _defaults = Substitute.For<INotificationDefaults>();

    public DeliverEmailCommandHandlerTests()
    {
        _provider.Name.Returns("Test");
        _defaults.EmailRequestedTopic.Returns("requested");
        _defaults.DeadLetterTopic.Returns("dlq");
        _defaults.MaximumDeliveryAttempts.Returns(2);
        _renderer.RenderAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(new RenderedEmail("Rendered subject", "<p>Hi</p>", "Hi"));
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Successful_send_marks_email_sent_without_dead_letter()
    {
        var n = await AddQueuedAsync();
        Provider(EmailProviderResult.Success(202));

        await Handle(n);

        Assert.Equal(NotificationStatus.Sent, n.Status);
        Assert.Equal(1, n.AttemptCount);
        Assert.Equal(Now, n.LastAttemptAt);
        Assert.Empty(_db.OutboxMessages);
        await _provider.Received(1).SendAsync(Arg.Is<EmailMessage>(m => m.Subject == "Rendered subject" && m.To.Count == 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transient_failure_with_attempts_remaining_schedules_retry()
    {
        var n = await AddQueuedAsync();
        Provider(EmailProviderResult.Failure(EmailFailureCategory.Transient, "Smtp.Timeout", "Timed out."));

        await Handle(n);

        Assert.Equal(NotificationStatus.RetryScheduled, n.Status);
        Assert.Equal(Now.AddMinutes(1), n.NextAttemptAt);
        Assert.Equal("Smtp.Timeout", n.LastErrorCode);
        Assert.Empty(_db.OutboxMessages);
    }

    [Fact]
    public async Task Transient_failure_on_last_attempt_is_dead_lettered()
    {
        var n = await AddQueuedAsync();
        Provider(EmailProviderResult.Failure(EmailFailureCategory.Transient, "Smtp.Timeout", "Timed out."));

        await Handle(n);
        await Handle(n);

        Assert.Equal(NotificationStatus.DeadLettered, n.Status);
        Assert.Equal(2, n.AttemptCount);
        var dead = Assert.Single(_db.OutboxMessages);
        Assert.Equal("dlq", dead.Topic);
        Assert.Equal(n.MessageId, dead.MessageKey);
        var payload = JsonSerializer.Deserialize<EmailDeadLetteredV1>(dead.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(("Transient", "Smtp.Timeout", 2, n.Id), (payload.FailureCategory, payload.ErrorCode, payload.AttemptCount, payload.NotificationId));
    }

    [Fact]
    public async Task Permanent_failure_is_dead_lettered_immediately()
    {
        var n = await AddQueuedAsync();
        Provider(EmailProviderResult.Failure(EmailFailureCategory.Permanent, "Smtp.Rejected", "Rejected."));

        await Handle(n);

        Assert.Equal(NotificationStatus.DeadLettered, n.Status);
        Assert.Equal(Now, n.FailedAt);
        Assert.Single(_db.OutboxMessages);
    }

    [Fact]
    public async Task Unexpected_provider_exception_is_recorded_as_a_bounded_retry()
    {
        var n = await AddQueuedAsync();
        _provider.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).ThrowsAsync(new FormatException("bad sender"));

        await Handle(n);

        Assert.Equal(NotificationStatus.RetryScheduled, n.Status);
        Assert.Equal("Delivery.Unexpected", n.LastErrorCode);
        Assert.Equal(1, n.AttemptCount);
    }

    [Fact]
    public async Task Missing_attachment_fails_permanently_without_calling_the_provider()
    {
        var n = await AddQueuedAsync(new EmailContent
        {
            To = [new EmailAddress("to@example.com")],
            Attachments = [new EmailAttachmentReference(Guid.NewGuid())]
        });

        await Handle(n);

        Assert.Equal(NotificationStatus.DeadLettered, n.Status);
        Assert.Equal("Template.Invalid", n.LastErrorCode);
        Assert.Null(n.LastAttemptAt);
        await _provider.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    [Fact]
    public async Task Email_that_is_no_longer_due_is_left_unchanged()
    {
        var n = await AddQueuedAsync();
        n.MarkSent(Now);
        await _db.SaveChangesAsync();

        await Handle(n);

        Assert.Equal(0, n.AttemptCount);
        await _provider.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    private void Provider(EmailProviderResult result) =>
        _provider.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).Returns(result);

    private Task Handle(EmailNotification n) =>
        new DeliverEmailCommandHandler(_db, _provider, _renderer, _defaults, new IntegrationEventJsonSerializer(),
                new FakeDateTime(Now), new RetryPolicy(), new FakeLogger<DeliverEmailCommandHandler>())
            .Handle(new DeliverEmailCommand(n.Id), CancellationToken.None);

    private async Task<EmailNotification> AddQueuedAsync(EmailContent? content = null)
    {
        var n = EmailNotification.Create(Guid.NewGuid(), "corr", Guid.NewGuid().ToString(), "sender@example.com", null,
            "payment-confirmed", "{}", null, Now);
        n.SetIntegrationContext("otc-api", "hash",
            JsonSerializer.Serialize(content ?? new EmailContent { To = [new EmailAddress("to@example.com")] }));
        n.MarkQueued(Now);
        _db.EmailNotifications.Add(n);
        await _db.SaveChangesAsync();
        return n;
    }
}
