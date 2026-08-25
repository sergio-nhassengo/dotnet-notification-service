
using System.Threading;
using System.Threading.Tasks;

namespace Application.Common.Behaviours;
using Application.Common.Security;
using MediatR;
using Microsoft.Extensions.Logging;


public class LoggingBehaviour<TRequest, TResponse>(
    ILogger<LoggingBehaviour<TRequest, TResponse>> logger,
    ICurrentUserService currentUserService)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Handling {RequestName} for user {UserId}: {@Request}",
            typeof(TRequest).Name,
            currentUserService.UserId ?? "anonymous",
            request);

        return next();
    }
}
