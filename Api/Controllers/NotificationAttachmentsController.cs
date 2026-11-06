using Application.Common.Interfaces;
using Application.Notifications.Abstractions;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MPDCApiTemplate.Controllers;

[ApiController]
[Route("api/v1/notifications/attachments")]
[Authorize]
public sealed class NotificationAttachmentsController(IApplicationDbContext db, INotificationCaller caller,
    INotificationApplications applications, IDateTime clock) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        var app = applications.Find(caller.ApplicationId);
        if (app is null || app.MaximumAttachments == 0) 
            return Forbid();
        if (file.Length <= 0 || file.Length > app.MaximumAttachmentBytes) 
            return BadRequest("Attachment size exceeds the application limit.");
        var name = file.FileName;
        
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || name.IndexOfAny(['/', '\\', '\r', '\n', '\0']) >= 0)
            return BadRequest("Use a file name without a path.");
        var types = new[] { "application/pdf", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.ms-excel", "text/csv", "text/plain", "image/png", "image/jpeg" };
        if (!types.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase)) 
            return BadRequest("Unsupported attachment content type.");
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        var attachment = new NotificationAttachment
        {
            Id = Guid.NewGuid(), ApplicationId = app.Id, FileName = name, ContentType = file.ContentType,
            Content = buffer.ToArray(), CreatedAt = clock.Now
        };
        db.NotificationAttachments.Add(attachment);
        await db.SaveChangesAsync(ct);
        return StatusCode(201, new { attachmentId = attachment.Id, attachment.FileName, attachment.ContentType, size = attachment.Content.Length });
    }
}
