using Application.Common.Interfaces;
using Application.Notifications.Integration;
using Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Queries.GetEmailStatus;

public sealed record FindEmailsQuery(string? BusinessReference, string? CorrelationId, string? IdempotencyKey,
    string? ApplicationId, int Skip = 0, int Take = 50) : IRequest<Result<IReadOnlyList<NotificationSummary>>>;
public sealed record NotificationSummary(Guid NotificationId, string ApplicationId, string? BusinessReference,
    string CorrelationId, string Status, int AttemptCount, DateTimeOffset RequestedAt);
public sealed class FindEmailsQueryHandler(IApplicationDbContext db, INotificationCaller caller,
    INotificationApplications applications) : IRequestHandler<FindEmailsQuery, Result<IReadOnlyList<NotificationSummary>>>
{
    public async Task<Result<IReadOnlyList<NotificationSummary>>> Handle(FindEmailsQuery request, CancellationToken ct)
    {
        if (request.Skip < 0 || request.Take is < 1 or > 100 ||
            new[] { request.BusinessReference, request.CorrelationId, request.IdempotencyKey }.Any(x => x?.Length > 200))
            return Result.Failure<IReadOnlyList<NotificationSummary>>(Error.Validation("Query", "Invalid search or pagination limits."));
        if (!caller.IsAdmin && (applications.Find(caller.ApplicationId) is null || request.ApplicationId is not null && request.ApplicationId != caller.ApplicationId))
            return Result.Failure<IReadOnlyList<NotificationSummary>>(Error.Unauthorized("ApplicationId", "Application access is not permitted."));
        var query = db.EmailNotifications.AsNoTracking();
        var appId = caller.IsAdmin ? request.ApplicationId : caller.ApplicationId;
        if (appId is not null) 
            query = query.Where(x => x.ApplicationId == appId);
        if (request.BusinessReference is not null) 
            query = query.Where(x => x.BusinessReference == request.BusinessReference);
        if (request.CorrelationId is not null) 
            query = query.Where(x => x.CorrelationId == request.CorrelationId);
        if (request.IdempotencyKey is not null) 
            query = query.Where(x => x.IdempotencyKey == request.IdempotencyKey);
        return await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip(request.Skip).Take(request.Take)
            .Select(x => new NotificationSummary(x.Id, x.ApplicationId, x.BusinessReference, x.CorrelationId,
                x.Status.ToString(), x.AttemptCount, x.RequestedAt)).ToListAsync(ct);
    }
}
