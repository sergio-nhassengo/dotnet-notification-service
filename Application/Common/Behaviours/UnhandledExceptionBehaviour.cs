using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Common.Behaviours;

public class UnhandledExceptionBehaviour<TRequest, TResponse>(ILogger<UnhandledExceptionBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex) when (ex is not Exceptions.ValidationException)
        {
            logger.LogError(ex, "Unhandled exception for request {RequestName} {@Request}", typeof(TRequest).Name, request);
            throw;
        }
    }
}
