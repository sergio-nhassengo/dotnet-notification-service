using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Features.TodoLists.Commands.CreateTodoList;
using Application.Features.TodoLists.Commands.DeleteTodoList;
using Application.Features.TodoLists.Commands.UpdateTodoList;
using Application.Features.TodoLists.Queries.GetTodoListById;
using Application.Features.TodoLists.Queries.GetTodoLists;
using Infrastructure.Extensions.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MPDCApiTemplate.Controllers;


public class TodoListsController : BaseController
{
    [HttpGet]
    public async Task<ActionResult<List<TodoListDto>>> GetTodoLists(CancellationToken cancellationToken)
    {
        var result = await this.Mediator.Send(new GetTodoListsQuery(), cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TodoListDto>> GetTodoList(int id, CancellationToken cancellationToken)
    {
        var result = await this.Mediator.Send(new GetTodoListByIdQuery(id), cancellationToken);
        return HandleResult(result);
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.RequireAdmin)]
    public async Task<ActionResult<int>> CreateTodoList(CreateTodoListCommand command, CancellationToken cancellationToken)
    {
        var result = await this.Mediator.Send(command, cancellationToken);
        return HandleCreatedResult(result, nameof(GetTodoList), id => new { id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTodoList(int id, UpdateTodoListCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest();
        }

        var result = await this.Mediator.Send(command, cancellationToken);
        return HandleResult(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTodoList(int id, CancellationToken cancellationToken)
    {
        var result = await this.Mediator.Send(new DeleteTodoListCommand(id), cancellationToken);
        return HandleResult(result);
    }
}
