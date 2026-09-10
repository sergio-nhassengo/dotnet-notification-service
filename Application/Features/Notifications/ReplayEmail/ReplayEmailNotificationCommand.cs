using System.Text.Json;
using Application.Notifications.Integration;
using Application.Common.Interfaces;
using Application.Common.Security;
using Domain.Common;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Commands.ReplayEmail;

public sealed record ReplayEmailNotificationCommand(Guid NotificationId) : IRequest<Result>;

public sealed class ReplayEmailNotificationCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTime clock, INotificationApplications applications, IEmailPreflight preflight)
    : IRequestHandler<ReplayEmailNotificationCommand, Result>
{
    public async Task<Result> Handle(ReplayEmailNotificationCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole("Admin"))
            return Result.Failure(Error.Unauthorized("Notification.Replay", "Admin access is required."));
        var entity = await db.EmailNotifications.SingleOrDefaultAsync(x => x.Id == request.NotificationId, cancellationToken);
        if (entity is null || entity.Status is not (NotificationStatus.DeadLettered or NotificationStatus.PermanentlyFailed))
            return Result.Failure(Error.Conflict("Notification.NotReplayable", "The notification does not exist or is not replayable."));

        var app = applications.Find(entity.ApplicationId);
        if (app is null) 
            return Result.Failure(Error.Validation("Application", "The application is disabled or unavailable."));
        var content = entity.ContentJson is null ? new EmailContent { To = [new(entity.RecipientEmail, entity.RecipientName)] }
            : JsonSerializer.Deserialize<EmailContent>(entity.ContentJson)!;
        var variables = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.TemplateVariables) ?? [];
        var validation = await preflight.ValidateAsync(app, entity.TemplateId, variables, entity.Subject, content, cancellationToken);
        if (validation is not null) 
            return Result.Failure(validation);
        var now = clock.Now;
        var actor = currentUser.UserId ?? "unknown";
        db.NotificationReplays.Add(new NotificationReplay
        {
            Id = Guid.NewGuid(),
            NotificationId = entity.Id,
            RequestedBy = actor[..Math.Min(actor.Length, 200)],
            RequestedAt = now,
            PreviousAttemptCount = entity.AttemptCount
        });
        entity.Replay(now);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
