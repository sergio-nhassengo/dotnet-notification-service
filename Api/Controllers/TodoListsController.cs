using Application.Features.TodoLists.Commands.CreateTodoList;
using Application.Features.TodoLists.Queries.GetTodoLists;
using Infra.Extensions.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiTemplate.Controllers;

[ApiController]
[Route("todo-lists")]
public class TodoListsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TodoListDto>>> GetTodoLists(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTodoListsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    //[Authorize(Policy = AuthPolicies.RequireAdmin)]
    public async Task<ActionResult<int>> CreateTodoList(CreateTodoListCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetTodoLists), new { id }, id);
    }
}
