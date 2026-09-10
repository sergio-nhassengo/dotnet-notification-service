using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Messaging;
using Application.Common.Notifications;
using Application.Common.Persistence;
using Application.Notifications.Contracts;
using Application.Notifications.Integration;
using Domain.Common;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Commands.CreateEmail;

public sealed record EmailRecipient(string Email, string? Name);
public sealed record CreateEmailNotificationCommand(string IdempotencyKey, string CorrelationId,
    EmailRecipient? Recipient, string? TemplateId,
    IReadOnlyDictionary<string, string>? Variables, string? Subject, DateTimeOffset? ScheduledAt)
    : IRequest<Result<EmailAcceptedResponse>>
{
    public string? BusinessReference { get; init; }
    public IReadOnlyList<EmailAddress> To { get; init; } = [];
    public IReadOnlyList<EmailAddress> Cc { get; init; } = [];
    public IReadOnlyList<EmailAttachmentReference> Attachments { get; init; } = [];
    public string? HtmlBody { get; init; }
    public string? TextBody { get; init; }
    public EmailContent Content() => new()
    {
        To = Recipient is not null ? [new(Recipient.Email, Recipient.Name)] : To,
        Cc = Cc, Attachments = Attachments, HtmlBody = HtmlBody, TextBody = TextBody
    };
}
public sealed record EmailAcceptedResponse(Guid NotificationId, Guid MessageId, string Status);

public sealed class CreateEmailNotificationCommandHandler(
    IApplicationDbContext db, IntegrationEventJsonSerializer serializer,
    INotificationDefaults defaults, IDateTime clock, INotificationCaller caller,
    INotificationApplications applications, IEmailPreflight preflight)
    : IRequestHandler<CreateEmailNotificationCommand, Result<EmailAcceptedResponse>>
{
    public async Task<Result<EmailAcceptedResponse>> Handle(CreateEmailNotificationCommand request, CancellationToken ct)
    {
        var validation = new CreateEmailNotificationCommandValidator().Validate(request);
        if (!validation.IsValid)
            return Result.Failure<EmailAcceptedResponse>(ValidationError.FromErrors(validation.Errors.Select(x => Error.Validation(x.PropertyName, x.ErrorMessage)).ToArray()));
        var app = applications.Find(caller.ApplicationId);
        if (app is null) 
            return Result.Failure<EmailAcceptedResponse>(Error.Unauthorized("Notification.Application", "Application is not enabled."));
        var content = request.Content();
        var variables = request.Variables ?? new Dictionary<string, string>();
        var hash = EmailRequestFingerprint.Compute(request);
        var existing = await db.EmailNotifications.AsNoTracking().SingleOrDefaultAsync(x => x.ApplicationId == app.Id && x.IdempotencyKey == request.IdempotencyKey, ct);
        if (existing is not null) 
            return Existing(existing, request);
        var error = await preflight.ValidateAsync(app, request.TemplateId, variables, request.Subject, content, ct);
        if (error is not null) 
            return Result.Failure<EmailAcceptedResponse>(error);

        var now = clock.Now;
        var messageId = Guid.NewGuid();
        var first = content.To[0];
        var integrationEvent = new EmailRequestedV1(messageId, request.CorrelationId, request.IdempotencyKey, first.Email, first.Name, request.TemplateId ?? "", variables, request.Subject, now, request.ScheduledAt)
        {
            ApplicationId = app.Id, BusinessReference = request.BusinessReference, RequestHash = hash, Content = content
        };
        var notification = EmailNotification.Create(messageId, request.CorrelationId, request.IdempotencyKey,
            first.Email, first.Name, app.SenderEmail, app.SenderName, app.ReplyTo,
            request.TemplateId ?? "", JsonSerializer.Serialize(variables), request.Subject,
            NotificationSource.RestApi, now, request.ScheduledAt, now);
        notification.SetIntegrationContext(app.Id, request.BusinessReference, hash, JsonSerializer.Serialize(content));
        db.EmailNotifications.Add(notification);
        if (applications.UseKafka)
        {
            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(), EventType = EmailRequestedV1.EventType, SchemaVersion = EmailRequestedV1.SchemaVersion,
                MessageKey = messageId, Topic = defaults.EmailRequestedTopic, Payload = serializer.Serialize(integrationEvent),
                Headers = serializer.Serialize(new Dictionary<string, string>
                {
                    ["correlation-id"] = request.CorrelationId, ["causation-id"] = messageId.ToString(),
                    ["schema-version"] = "1", ["application-id"] = app.Id
                }),
                OccurredAt = now, NextAttemptAt = now, CreatedAt = now
            });
        }
        else notification.MarkQueued(now);
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
        !EmailRequestFingerprint.Matches(existing, request)
            ? Result.Failure<EmailAcceptedResponse>(Error.Conflict("Notification.IdempotencyConflict", "The idempotency key was already used for different content."))
            : new EmailAcceptedResponse(existing.Id, existing.MessageId, existing.Status.ToString());
}
