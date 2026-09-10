using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Security;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Common.Behaviours;

public class PerformanceBehaviour<TRequest, TResponse>(
    ILogger<PerformanceBehaviour<TRequest, TResponse>> logger,
    ICurrentUserService currentUserService)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private const int SlowRequestThresholdMilliseconds = 500;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var response = await next();

        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > SlowRequestThresholdMilliseconds)
        {
            if (typeof(TRequest).Namespace?.Contains(".Features.Notifications.", StringComparison.Ordinal) == true)
            {
                logger.LogWarning("Slow request: {RequestName} ({ElapsedMilliseconds}ms) for user {UserId}",
                    typeof(TRequest).Name, stopwatch.ElapsedMilliseconds, currentUserService.UserId ?? "anonymous");
                return response;
            }
            logger.LogWarning(
                "Slow request: {RequestName} ({ElapsedMilliseconds}ms) for user {UserId} {@Request}",
                typeof(TRequest).Name,
                stopwatch.ElapsedMilliseconds,
                currentUserService.UserId ?? "anonymous",
                request);
        }

        return response;
    }
}
