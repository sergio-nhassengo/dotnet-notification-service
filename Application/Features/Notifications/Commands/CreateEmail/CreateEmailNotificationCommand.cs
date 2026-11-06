using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Persistence;
using Application.Notifications.Abstractions;
using Application.Notifications.Contracts;
using Application.Notifications.Idempotency;
using Application.Notifications.Models;
using Domain.Common;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Commands.CreateEmail;

public sealed record CreateEmailNotificationCommand(string IdempotencyKey, string CorrelationId,
    EmailRecipient? Recipient, string? TemplateId,
    IReadOnlyDictionary<string, string>? Variables, string? Subject)
    : IRequest<Result<EmailAcceptedResponse>>
{
    public IReadOnlyList<EmailAddress> To { get; init; } = [];
    public IReadOnlyList<EmailAddress> Cc { get; init; } = [];
    public string? HtmlBody { get; init; }
    public string? TextBody { get; init; }
    public EmailContent Content() => new()
    {
        To = Recipient is not null ? [new(Recipient.Email, Recipient.Name)] : To,
        Cc = Cc, HtmlBody = HtmlBody, TextBody = TextBody
    };
    public EmailRequestSnapshot ToSnapshot() => new(CorrelationId, TemplateId ?? "",
        Variables ?? new Dictionary<string, string>(), Subject, Content());
}

public sealed class CreateEmailNotificationCommandHandler(
    IApplicationDbContext db, IntegrationEventJsonSerializer serializer,
    INotificationDefaults defaults, IDateTime clock, INotificationCaller caller,
    INotificationApplications applications, IEmailPreflight preflight)
    : IRequestHandler<CreateEmailNotificationCommand, Result<EmailAcceptedResponse>>
{
    public async Task<Result<EmailAcceptedResponse>> Handle(CreateEmailNotificationCommand request, CancellationToken ct)
    {
        // Request shape is validated by ValidationBehaviour before this handler runs.
        var app = applications.Find(caller.ApplicationId);
        if (app is null) 
            return Result.Failure<EmailAcceptedResponse>(Error.Unauthorized("Notification.Application", "Application is not enabled."));
        var content = request.Content();
        var variables = request.Variables ?? new Dictionary<string, string>();
        var hash = EmailRequestFingerprint.Compute(request.ToSnapshot());
        var existing = await db.EmailNotifications.AsNoTracking().SingleOrDefaultAsync(x => x.ApplicationId == app.Id && x.IdempotencyKey == request.IdempotencyKey, ct);
        if (existing is not null) 
            return Existing(existing, request);
        var error = await preflight.ValidateAsync(app, request.TemplateId, variables, request.Subject, content, ct);
        if (error is not null) 
            return Result.Failure<EmailAcceptedResponse>(error);

        var now = clock.Now;
        var messageId = Guid.NewGuid();
        var integrationEvent = new EmailRequestedV1(messageId, request.CorrelationId, request.IdempotencyKey, request.TemplateId ?? "", variables, request.Subject, now)
        {
            ApplicationId = app.Id, RequestHash = hash, Content = content
        };
        var notification = EmailNotification.Create(messageId, request.CorrelationId, request.IdempotencyKey,
            app.SenderEmail, app.SenderName,
            request.TemplateId ?? "", JsonSerializer.Serialize(variables), request.Subject,
            now);
        notification.SetIntegrationContext(app.Id, hash, JsonSerializer.Serialize(content));
        db.EmailNotifications.Add(notification);
        {
            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageKey = messageId, Topic = defaults.EmailRequestedTopic, Payload = serializer.Serialize(integrationEvent),
                Headers = serializer.Serialize(new Dictionary<string, string>
                {
                    ["correlation-id"] = request.CorrelationId, ["causation-id"] = messageId.ToString(),
                    ["application-id"] = app.Id
                }),
                OccurredAt = now, NextAttemptAt = now, CreatedAt = now
            });
        }
        try
        {
            await db.SaveChangesAsync(ct);
            return new EmailAcceptedResponse(notification.Id, notification.MessageId, notification.Status.ToString());
        }
        catch (DbUpdateException exception) when (UniqueConstraintViolation.IsExpected(exception))
        {
            db.ClearTrackedChanges();
            existing = await db.EmailNotifications.AsNoTracking().SingleAsync(
                x => x.ApplicationId == app.Id && x.IdempotencyKey == request.IdempotencyKey, ct);
            return Existing(existing, request);
        }
    }
    private static Result<EmailAcceptedResponse> Existing(EmailNotification existing, CreateEmailNotificationCommand request) =>
        !EmailRequestFingerprint.Matches(existing, request.IdempotencyKey, request.ToSnapshot())
            ? Result.Failure<EmailAcceptedResponse>(Error.Conflict("Notification.IdempotencyConflict", "The idempotency key was already used for different content."))
            : new EmailAcceptedResponse(existing.Id, existing.MessageId, existing.Status.ToString());
}
