using Application.Notifications.Integration;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;
using Application.Common.Email;
using Application.Common.Interfaces;
using Application.Common.Messaging;
using Application.Common.Notifications;
using Application.Common.Retry;
using Application.Features.Notifications.Commands.Delivery;
using Application.Features.Notifications.Queries.Delivery;
using Application.Notifications.Contracts;
using Application.Notifications.Models;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Notifications.Options;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Workers;

public sealed class EmailDeliveryWorker(IServiceScopeFactory scopes, IOptions<EmailDeliveryOptions> deliveryOptions,
    INotificationDefaults defaults, IntegrationEventJsonSerializer serializer, IDateTime clock,
    RetryPolicy retryPolicy, ILogger<EmailDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var idleDelay = TimeSpan.FromSeconds(deliveryOptions.Value.PollingIntervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var found = await ProcessNext(stoppingToken);
                if (!found) await Task.Delay(idleDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Email delivery worker cycle failed");
                try { await Task.Delay(idleDelay, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
    private async Task<bool> ProcessNext(CancellationToken ct)
    {
        EmailNotification? notification;
        using (var scope = scopes.CreateScope()) notification = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new GetNextDueEmailDeliveryQuery(clock.Now), ct);
        if (notification is null) return false;
        await DeliverNotification(notification, ct);
        return true;
    }
    private async Task DeliverNotification(EmailNotification n, CancellationToken ct)
    {
        string providerName;
        var started = clock.Now; var sw = Stopwatch.StartNew();
        EmailProviderResult result;
        using (var deliveryScope = scopes.CreateScope())
        {
            var provider = deliveryScope.ServiceProvider.GetRequiredService<IEmailProvider>();
            var renderer = deliveryScope.ServiceProvider.GetRequiredService<FileEmailTemplateRenderer>();
            providerName = provider.Name;
            try
            {
                var variables = JsonSerializer.Deserialize<Dictionary<string, string>>(n.TemplateVariables) ?? [];
                var content = n.ContentJson is null ? new EmailContent { To = [new(n.RecipientEmail, n.RecipientName)] }
                    : JsonSerializer.Deserialize<EmailContent>(n.ContentJson)!;
                var rendered = content.HtmlBody is not null || content.TextBody is not null
                    ? new RenderedEmail(n.Subject!, content.HtmlBody ?? "", content.TextBody ?? "")
                    : await renderer.RenderAsync(n.TemplateId, variables, ct);
                if (content.Attachments.Count(x => x.ContentId is null) < rendered.MinimumAttachments)
                    throw new TemplateException("This template requires a document attachment.");
                var db = deliveryScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var ids = content.Attachments.Select(x => x.AttachmentId).ToArray();
                var stored = await db.NotificationAttachments.AsNoTracking()
                    .Where(x => x.ApplicationId == n.ApplicationId && ids.Contains(x.Id)).ToListAsync(ct);
                if (stored.Count != ids.Length) throw new TemplateException("A required attachment is unavailable.");
                result = await provider.SendAsync(new EmailMessage(n.MessageId, n.RecipientEmail, n.RecipientName,
                    n.SenderEmail, n.SenderName, n.ReplyTo, n.Subject ?? rendered.Subject, rendered.HtmlBody,
                    rendered.TextBody)
                {
                    To = content.To, Cc = content.Cc,
                    Attachments = content.Attachments.Select(reference =>
                    {
                        var file = stored.Single(x => x.Id == reference.AttachmentId);
                        return new DeliveryAttachment(file.FileName, file.ContentType, file.Content, reference.ContentId);
                    }).ToArray()
                }, ct);
            }
            catch (TemplateException ex)
            {
                result = EmailProviderResult.Failure(EmailFailureCategory.Permanent, "Template.Invalid", ex.Message);
            }
            catch (IOException)
            {
                result = EmailProviderResult.Failure(EmailFailureCategory.Transient, "Content.Unavailable", "Email content could not be read.");
            }
            catch (JsonException)
            {
                result = EmailProviderResult.Failure(EmailFailureCategory.Permanent, "Template.VariablesMalformed", "Template variables are malformed.");
            }
        }
        sw.Stop();
        var attempt = n.AttemptCount + 1;
        var retryable = IsRetryable(result.FailureCategory);
        var shouldRetry =
            retryable &&
            retryPolicy.HasAttemptsRemaining(
                attempt,
                deliveryOptions.Value.MaximumAttempts);
        DateTimeOffset? next = null;
        if (shouldRetry)
            next = retryPolicy.GetNextAttemptAt(clock.Now, attempt, result.RetryAfter);
        OutboxMessage? dlq = null;
        if (!result.IsSuccess && next is null && UsesKafka())
        {
            var dead = new EmailDeadLetteredV1(n.MessageId, n.CorrelationId, n.OriginalTopic ?? defaults.EmailRequestedTopic,
                n.OriginalPartition, n.OriginalOffset, result.FailureCategory.ToString(), result.ErrorCode ?? "Delivery.Failed",
                result.SafeErrorMessage ?? "Delivery failed.", attempt, clock.Now, n.Id);
            dlq = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = EmailDeadLetteredV1.EventType,
                SchemaVersion = EmailDeadLetteredV1.SchemaVersion,
                MessageKey = n.MessageId,
                Topic = defaults.DeadLetterTopic,
                Payload = serializer.Serialize(dead),
                Headers = serializer.Serialize(new Dictionary<string, string> { { "correlation-id", n.CorrelationId }, { "causation-id", n.MessageId.ToString() }, { "schema-version", "1" } }),
                OccurredAt = clock.Now,
                NextAttemptAt = clock.Now,
                CreatedAt = clock.Now
            };
        }
        using (var resultScope = scopes.CreateScope())
            await resultScope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new RecordEmailDeliveryResultCommand(n.Id, providerName, started, result, clock.Now, next, dlq), ct);
        logger.Log(result.IsSuccess ? LogLevel.Information : LogLevel.Warning,
            "Email delivery {Outcome}: NotificationId {NotificationId}, MessageId {MessageId}, CorrelationId {CorrelationId}, TemplateId {TemplateId}, Provider {Provider}, AttemptNumber {AttemptNumber}, Duration {DurationMs}ms",
            result.IsSuccess ? "sent" : result.FailureCategory.ToString(), n.Id, n.MessageId, n.CorrelationId, n.TemplateId, providerName, attempt, sw.Elapsed.TotalMilliseconds);
        if (result.FailureCategory == EmailFailureCategory.Configuration)
            logger.LogCritical("Email provider configuration failure: NotificationId {NotificationId}, MessageId {MessageId}, Provider {Provider}, ErrorCode {ErrorCode}",
                n.Id, n.MessageId, providerName, result.ErrorCode);
    }

    private bool UsesKafka()
    {
        using var scope = scopes.CreateScope();
        return scope.ServiceProvider.GetRequiredService<INotificationApplications>().UseKafka;
    }

    private static bool IsRetryable(EmailFailureCategory failureCategory) =>
        failureCategory is EmailFailureCategory.Transient or EmailFailureCategory.RateLimited;
}
