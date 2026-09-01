using System;
using System.Linq;
using Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace MPDCApiTemplate.Controllers;

[Route("api/[controller]")]
[ApiController]
public class BaseController: ControllerBase
{
    protected IMediator Mediator => HttpContext.RequestServices.GetRequiredService<IMediator>();

    protected ActionResult HandleResult(Result result)
    {
        return result.IsSuccess ? NoContent() : ToProblemResult(result.Error);
    }

    protected ActionResult<TValue> HandleResult<TValue>(Result<TValue> result)
    {
        return result.IsSuccess ? Ok(result.Value) : ToProblemResult(result.Error);
    }

    protected ActionResult<TValue> HandleCreatedResult<TValue>(Result<TValue> result, string actionName, Func<TValue, object> routeValues)
    {
        return result.IsSuccess
            ? CreatedAtAction(actionName, routeValues(result.Value), result.Value)
            : ToProblemResult(result.Error);
    }

    private ObjectResult ToProblemResult(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = error is ValidationError ? "Validation error" : error.Message,
            Instance = HttpContext.Request.Path
        };

        if (error is ValidationError validationError)
        {
            problemDetails.Extensions["errors"] = validationError.Errors
                .GroupBy(e => e.Code, e => e.Message)
                .ToDictionary(g => g.Key, g => g.ToArray());
        }

        problemDetails.Extensions["traceId"] = HttpContext.TraceIdentifier;

        return new ObjectResult(problemDetails) { StatusCode = statusCode };
    }
}
