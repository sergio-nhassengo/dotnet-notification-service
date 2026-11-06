using System.Diagnostics;
using System.Text.Json;
using Application.Common.Behaviours;
using Application.Common.Interfaces;
using Application.Common.Retry;
using Application.Notifications.Abstractions;
using Application.Notifications.Contracts;
using Application.Notifications.Models;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Notifications.Commands.DeliverEmail;

/// <summary>
/// Makes one delivery attempt for a due email and records its outcome: sent, retry scheduled, or permanently
/// failed with a dead-letter event. The state change and any dead-letter outbox row are saved together.
/// </summary>
public sealed record DeliverEmailCommand(Guid NotificationId) : IRequest, ILongRunningRequest;

public sealed class DeliverEmailCommandHandler(IApplicationDbContext db, IEmailProvider provider,
    IEmailTemplateRenderer renderer, INotificationDefaults defaults, IntegrationEventJsonSerializer serializer,
    IDateTime clock, RetryPolicy retryPolicy, ILogger<DeliverEmailCommandHandler> logger)
    : IRequestHandler<DeliverEmailCommand>
{
    public async Task Handle(DeliverEmailCommand request, CancellationToken ct)
    {
        var n = await db.EmailNotifications.SingleOrDefaultAsync(x => x.Id == request.NotificationId, ct);
        if (n is null || n.Status is not (NotificationStatus.Queued or NotificationStatus.RetryScheduled))
            return;

        DateTimeOffset? providerCalledAt = null;
        var sw = Stopwatch.StartNew();
        EmailProviderResult result;
        try
        {
            var message = await PrepareMessageAsync(n, ct);
            providerCalledAt = clock.Now;
            result = await provider.SendAsync(message, ct);
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
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Record unexpected failures as a bounded transient attempt. Letting them escape would leave the
            // email due and first in CreatedAt order, so it would be retried every poll and block the queue.
            logger.LogError(ex, "Unexpected email delivery failure: NotificationId {NotificationId}, MessageId {MessageId}",
                n.Id, n.MessageId);
            result = EmailProviderResult.Failure(EmailFailureCategory.Transient, "Delivery.Unexpected", "An unexpected delivery error occurred.");
        }
        sw.Stop();

        var now = clock.Now;
        var attempt = n.AttemptCount + 1;
        DateTimeOffset? next = !result.IsSuccess && RetryPolicy.IsRetryable(result.FailureCategory) &&
            retryPolicy.HasAttemptsRemaining(attempt, defaults.MaximumDeliveryAttempts)
                ? retryPolicy.GetNextAttemptAt(now, attempt, result.RetryAfter)
                : null;

        n.RecordAttempt(providerCalledAt, now);
        if (result.IsSuccess)
            n.MarkSent(now);
        else if (next is not null)
            n.ScheduleRetry(result.ErrorCode ?? "TransientFailure", result.SafeErrorMessage ?? "Delivery failed.", next.Value, now);
        else
        {
            n.MarkPermanentlyFailed(result.ErrorCode ?? "PermanentFailure", result.SafeErrorMessage ?? "Delivery failed.", now);
            var dead = new EmailDeadLetteredV1(n.MessageId, n.CorrelationId, defaults.EmailRequestedTopic,
                null, null, result.FailureCategory.ToString(), result.ErrorCode ?? "Delivery.Failed",
                result.SafeErrorMessage ?? "Delivery failed.", attempt, now, n.Id);
            db.OutboxMessages.Add(DeadLetterOutbox.Create(dead, serializer, defaults, now));
            n.MarkDeadLettered(now);
        }
        await db.SaveChangesAsync(ct);

        logger.Log(result.IsSuccess ? LogLevel.Information : LogLevel.Warning,
            "Email delivery {Outcome}: NotificationId {NotificationId}, MessageId {MessageId}, CorrelationId {CorrelationId}, TemplateId {TemplateId}, Provider {Provider}, AttemptNumber {AttemptNumber}, Duration {DurationMs}ms, ProviderStatusCode {ProviderStatusCode}, ErrorCode {ErrorCode}, ErrorMessage {ErrorMessage}, NextAttemptAt {NextAttemptAt}",
            result.IsSuccess ? "sent" : result.FailureCategory.ToString(), n.Id, n.MessageId, n.CorrelationId, n.TemplateId,
            provider.Name, attempt, sw.Elapsed.TotalMilliseconds, result.StatusCode, result.ErrorCode, result.SafeErrorMessage, next);
        if (result.FailureCategory == EmailFailureCategory.Configuration)
            logger.LogCritical("Email provider configuration failure: NotificationId {NotificationId}, MessageId {MessageId}, Provider {Provider}, ErrorCode {ErrorCode}",
                n.Id, n.MessageId, provider.Name, result.ErrorCode);
    }

    private async Task<EmailMessage> PrepareMessageAsync(EmailNotification n, CancellationToken ct)
    {
        var variables = JsonSerializer.Deserialize<Dictionary<string, string>>(n.TemplateVariables) ?? [];
        var content = n.ContentJson is null ? null : JsonSerializer.Deserialize<EmailContent>(n.ContentJson);
        if (content is null || content.To.Count == 0)
            throw new TemplateException("Email content must contain at least one recipient.");
        var rendered = content.HtmlBody is not null || content.TextBody is not null
            ? new RenderedEmail(n.Subject!, content.HtmlBody ?? "", content.TextBody ?? "")
            : await renderer.RenderAsync(n.TemplateId, variables, ct);
        return new EmailMessage(n.MessageId, content.To[0].Email, content.To[0].Name,
            n.SenderEmail, n.SenderName, n.Subject ?? rendered.Subject, rendered.HtmlBody, rendered.TextBody)
        {
            To = content.To,
            Cc = content.Cc
        };
    }
}
