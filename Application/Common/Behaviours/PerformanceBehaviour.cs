using System.Diagnostics;
using Application.Notifications.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Common.Behaviours;

public class PerformanceBehaviour<TRequest, TResponse>(
    ILogger<PerformanceBehaviour<TRequest, TResponse>> logger,
    INotificationCaller caller)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private const int SlowRequestThresholdMilliseconds = 500;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var response = await next();

        stopwatch.Stop();

        if (request is not ILongRunningRequest && stopwatch.ElapsedMilliseconds > SlowRequestThresholdMilliseconds)
        {
            if (typeof(TRequest).Namespace?.Contains(".Features.Notifications.", StringComparison.Ordinal) == true)
            {
                logger.LogWarning("Slow request: {RequestName} ({ElapsedMilliseconds}ms) for application {ApplicationId}",
                    typeof(TRequest).Name, stopwatch.ElapsedMilliseconds, ApplicationId);
                return response;
            }
            logger.LogWarning(
                "Slow request: {RequestName} ({ElapsedMilliseconds}ms) for application {ApplicationId} {@Request}",
                typeof(TRequest).Name,
                stopwatch.ElapsedMilliseconds,
                ApplicationId,
                request);
        }

        return response;
    }

    // Background workers run outside an HTTP request, so there is no authenticated application.
    private string ApplicationId => string.IsNullOrEmpty(caller.ApplicationId) ? "anonymous" : caller.ApplicationId;
}
