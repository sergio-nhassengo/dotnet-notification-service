using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Domain.Common;
using FluentValidation;
using MediatR;

namespace Application.Common.Behaviours;

public class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count == 0)
        {
            return await next();
        }

        var error = ValidationError.FromErrors(failures
            .Select(f => Error.Validation(f.PropertyName, f.ErrorMessage))
            .ToArray());

        return CreateFailureResult<TResponse>(error);
    }

    private static TResult CreateFailureResult<TResult>(ValidationError error)
        where TResult : Result
    {
        if (typeof(TResult) == typeof(Result))
        {
            return (TResult)(object)Result.Failure(error);
        }

        var valueType = typeof(TResult).GetGenericArguments()[0];
        var failureMethod = typeof(Result)
            .GetMethods()
            .First(m => m is { Name: nameof(Result.Failure), IsGenericMethodDefinition: true })
            .MakeGenericMethod(valueType);

        return (TResult)failureMethod.Invoke(null, [error])!;
    }
}
