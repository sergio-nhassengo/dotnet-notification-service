using Application.Common.Interfaces;
using Application.Notifications.Abstractions;
using Application.Notifications.Models;
using Domain.Common;
using Infrastructure.Notifications.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Integration;

public sealed class EmailSubmissionValidator(IApplicationDbContext db, IEmailTemplateRenderer renderer, IOptions<EmailProviderOptions> provider) : IEmailPreflight
{
    public async Task<Error?> ValidateAsync(NotificationApplication app, string? templateId,
        IReadOnlyDictionary<string, string> variables, string? subject, EmailContent content, CancellationToken ct)
    {
        if (content.To.Count + content.Cc.Count > app.MaximumRecipients)
            return Error.Validation("Recipients", "Application recipient limit exceeded.");
        if (content.Attachments.Count > app.MaximumAttachments)
            return Error.Validation("Attachments", "Application attachment limit exceeded.");
        if (content.Attachments.Count > 0 && provider.Value.Provider.Equals("SmtpRelay", StringComparison.OrdinalIgnoreCase))
            return Error.Validation("Attachments", "SMTP relay attachment format has not been configured; use SMTP for attachments.");
        var raw = content.HtmlBody is not null || content.TextBody is not null;
        if (raw && !app.AllowRawContent) return Error.Forbidden("Content", "Raw content is not allowed for this application.");
        if (!raw)
        {
            if (!app.TemplateIds.Contains(templateId, StringComparer.Ordinal))
                return Error.Forbidden("TemplateId", "Template is not allowed for this application.");
            if (subject is not null && !app.AllowSubjectOverride)
                return Error.Validation("Subject", "Subject overrides are not allowed for this application.");
            try
            {
                var rendered = await renderer.RenderAsync(templateId!, variables, ct);
                if (content.Attachments.Count(x => x.ContentId is null) < rendered.MinimumAttachments)
                    return Error.Validation("Attachments", $"This template requires at least {rendered.MinimumAttachments} document attachment(s).");
                if (rendered.Subject.Length > 200 || rendered.Subject.IndexOfAny(['\r', '\n']) >= 0)
                    return Error.Validation("Subject", "Rendered subject is invalid.");
            }
            catch (TemplateException ex) { return Error.Validation("Template", ex.Message); }
        }
        var ids = content.Attachments.Select(x => x.AttachmentId).ToArray();
        var attachments = await db.NotificationAttachments.AsNoTracking()
            .Where(x => x.ApplicationId == app.Id && ids.Contains(x.Id))
            .Select(x => new { x.Id, Size = x.Content.Length }).ToListAsync(ct);
        if (attachments.Count != ids.Length) return Error.Validation("Attachments", "An attachment does not exist or belongs to another application.");
        if (attachments.Sum(x => (long)x.Size) > app.MaximumAttachmentBytes)
            return Error.Validation("Attachments", "Application total attachment size limit exceeded.");
        return null;
    }
}
