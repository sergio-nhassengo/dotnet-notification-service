using Application.Notifications.Models;

namespace Application.Notifications.Abstractions;

public interface IEmailTemplateRenderer
{
    Task<RenderedEmail> RenderAsync(string templateId, IReadOnlyDictionary<string, string> variables, CancellationToken ct);
}
