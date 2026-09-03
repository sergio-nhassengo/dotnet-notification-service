using System.Diagnostics;
using System.Text.Json;
using Application.Common.Interfaces;
using Application.Features.Notifications.Commands.Delivery;
using Application.Features.Notifications.Queries.Delivery;
using Application.Notifications.Contracts;
using Application.Notifications.Interfaces;
using Application.Notifications.Models;
using Application.Notifications.Retry;
using Application.Notifications.Security;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Notifications.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MediatR;

namespace Infrastructure.Notifications.Workers;

public sealed class EmailDeliveryWorker(IServiceScopeFactory scopes, IOptions<EmailDeliveryOptions> deliveryOptions,
    INotificationDefaults defaults, IIntegrationEventSerializer serializer, IDateTime clock,
    EmailRetryPolicy retryPolicy, ILogger<EmailDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(deliveryOptions.Value.PollingIntervalSeconds));
        do
        {
            try { await ProcessBatch(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Email delivery worker cycle failed"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
    private async Task ProcessBatch(CancellationToken ct)
    {
        IReadOnlyList<EmailNotification> rows;
        using (var scope = scopes.CreateScope()) rows = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new GetDueEmailDeliveryBatchQuery(deliveryOptions.Value.BatchSize, clock.Now), ct);
        await Parallel.ForEachAsync(rows, new ParallelOptions { MaxDegreeOfParallelism = deliveryOptions.Value.MaximumConcurrency, CancellationToken = ct }, ProcessOne);
    }
    private async ValueTask ProcessOne(EmailNotification n, CancellationToken ct)
    {
        string providerName;
        var started = clock.Now; var sw = Stopwatch.StartNew(); EmailProviderResult result;
        using (var deliveryScope = scopes.CreateScope())
        {
            var provider = deliveryScope.ServiceProvider.GetRequiredService<IEmailProvider>();
            var renderer = deliveryScope.ServiceProvider.GetRequiredService<IEmailTemplateRenderer>();
            providerName = provider.Name;
            try
            {
                var variables = JsonSerializer.Deserialize<Dictionary<string, string>>(n.TemplateVariables) ?? [];
                var rendered = await renderer.RenderAsync(n.TemplateId, n.TemplateVersion, variables, ct);
                result = await provider.SendAsync(new EmailMessage(n.MessageId, n.RecipientEmail, n.RecipientName,
                    n.SenderEmail, n.SenderName, n.ReplyTo, n.Subject ?? rendered.Subject, rendered.HtmlBody, rendered.TextBody), ct);
            }
            catch (TemplateException ex) { result = EmailProviderResult.Failure(EmailFailureCategory.Permanent, "Template.Invalid", SafeError.Sanitize(ex.Message)); }
            catch (JsonException) { result = EmailProviderResult.Failure(EmailFailureCategory.Permanent, "Template.VariablesMalformed", "Template variables are malformed."); }
        }
        sw.Stop();
        var attempt = n.AttemptCount + 1; DateTimeOffset? next = null;
        if (!result.IsSuccess && retryPolicy.ShouldRetry(result.FailureCategory, attempt, deliveryOptions.Value.MaximumAttempts))
            next = retryPolicy.NextAttempt(clock.Now, attempt + 1, result.RetryAfter, Random.Shared.NextDouble() * .4 - .2);
        OutboxMessage? dlq = null;
        if (!result.IsSuccess && next is null)
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
}
