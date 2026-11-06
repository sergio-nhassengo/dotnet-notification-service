using System.Threading.Tasks;

namespace Application.Common.Behaviours;
using MediatR;
using Microsoft.Extensions.Logging;
using Application.Notifications.Abstractions;


public class LoggingBehaviour<TRequest, TResponse>(
    ILogger<LoggingBehaviour<TRequest, TResponse>> logger,
    INotificationCaller caller)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (typeof(TRequest).Namespace?.Contains(".Features.Notifications.", StringComparison.Ordinal) == true)
        {
            logger.LogInformation("Handling {RequestName} for application {ApplicationId}", typeof(TRequest).Name, ApplicationId);
            return next();
        }
        logger.LogInformation(
            "Handling {RequestName} for application {ApplicationId}: {@Request}",
            typeof(TRequest).Name,
            ApplicationId,
            request);

        return next();
    }

    // Background workers run outside an HTTP request, so there is no authenticated application.
    private string ApplicationId => string.IsNullOrEmpty(caller.ApplicationId) ? "anonymous" : caller.ApplicationId;
}
