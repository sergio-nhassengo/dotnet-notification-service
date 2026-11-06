using Application.Features.Notifications.Commands.CreateEmail;
using Application.Features.Notifications.Queries.FindEmails;
using Application.Features.Notifications.Queries.GetEmailStatus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MPDCApiTemplate.Controllers;

[Route("api/v1/notifications")]
[Authorize]
[RequestSizeLimit(512 * 1024)]
public sealed class NotificationsController : BaseController
{
    [HttpPost("email")]
    public async Task<ActionResult<EmailAcceptedResponse>> Create(CreateEmailNotificationCommand command, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        if (result.IsFailure) return HandleResult(result);
        return AcceptedAtAction(nameof(Get), new { notificationId = result.Value.NotificationId }, result.Value);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationSummary>>> Find([FromQuery] FindEmailsQuery query, CancellationToken ct) =>
        HandleResult(await Mediator.Send(query, ct));

    [HttpGet("{notificationId:guid}")]
    public async Task<ActionResult<EmailNotificationStatusDto>> Get(Guid notificationId, CancellationToken ct) =>
        HandleResult(await Mediator.Send(new GetEmailStatusQuery(notificationId), ct));
}
