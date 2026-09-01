using Application.Common.Interfaces;
using Application.Common.Security;
using Application.Notifications.Interfaces;
using Domain.Common;
using MediatR;

namespace Application.Features.Notifications.Commands.ReplayEmail;

public sealed record ReplayEmailNotificationCommand(Guid NotificationId) : IRequest<Result>;

public sealed class ReplayEmailNotificationCommandHandler(INotificationStore store, ICurrentUserService currentUser, IDateTime clock)
    : IRequestHandler<ReplayEmailNotificationCommand, Result>
{
    public async Task<Result> Handle(ReplayEmailNotificationCommand request, CancellationToken cancellationToken)
    {
        var replayed = await store.ReplayAsync(request.NotificationId, currentUser.UserId ?? "unknown", clock.Now, cancellationToken);
        return replayed ? Result.Success() : Result.Failure(Error.Conflict("Notification.NotReplayable", "The notification does not exist or is not replayable."));
    }
}
