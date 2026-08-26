using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MPDCApiTemplate.ExceptionHandlers;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problemDetails = new ProblemDetails
        {
            Instance = httpContext.Request.Path
        };

        switch (exception)
        {
            case ValidationException validationException:
                problemDetails.Status = StatusCodes.Status400BadRequest;
                problemDetails.Title = "Validation error";
                problemDetails.Extensions["errors"] = validationException.Errors;
                logger.LogWarning(exception, "Validation exception. TraceId: {TraceId}, {Errors}", httpContext.TraceIdentifier, validationException.Errors);
                break;

            case NotFoundException:
                problemDetails.Status = StatusCodes.Status404NotFound;
                problemDetails.Title = exception.Message;
                logger.LogWarning(exception, "Not Found exception. TraceId: {TraceId}", httpContext.TraceIdentifier);
                break;

            case AuthenticationException:
                problemDetails.Status = StatusCodes.Status401Unauthorized;
                problemDetails.Title = exception.Message;
                logger.LogWarning(exception, "Authentication exception. TraceId: {TraceId}", httpContext.TraceIdentifier);
                break;

            default:
                problemDetails.Status = StatusCodes.Status500InternalServerError;
                problemDetails.Title = "An unexpected error occurred.";
                logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", httpContext.TraceIdentifier);
                break;
        }

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = problemDetails.Status.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
