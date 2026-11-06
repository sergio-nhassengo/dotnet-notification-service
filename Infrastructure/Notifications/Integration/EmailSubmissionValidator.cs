using Application.Notifications.Abstractions;
using Application.Notifications.Models;
using Domain.Common;

namespace Infrastructure.Notifications.Integration;

public sealed class EmailSubmissionValidator(IEmailTemplateRenderer renderer) : IEmailPreflight
{
    public async Task<Error?> ValidateAsync(NotificationApplication app, string? templateId,
        IReadOnlyDictionary<string, string> variables, string? subject, EmailContent content, CancellationToken ct)
    {
        if (content.To.Count + content.Cc.Count > app.MaximumRecipients)
            return Error.Validation("Recipients", "Application recipient limit exceeded.");
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
                if (rendered.Subject.Length > 200 || rendered.Subject.IndexOfAny(['\r', '\n']) >= 0)
                    return Error.Validation("Subject", "Rendered subject is invalid.");
            }
            catch (TemplateException ex) { return Error.Validation("Template", ex.Message); }
        }
        return null;
    }
}
