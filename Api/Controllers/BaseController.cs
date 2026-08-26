using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace MPDCApiTemplate.Controllers;

[Route("api/[controller]")]
[ApiController]
public class BaseController: ControllerBase
{
    protected IMediator Mediator => HttpContext.RequestServices.GetRequiredService<IMediator>();
}