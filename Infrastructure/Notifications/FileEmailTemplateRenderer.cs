using System.Net;
using System.Text.RegularExpressions;
using Application.Notifications.Interfaces;
using Application.Notifications.Models;
using Infrastructure.Notifications.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications;

public sealed partial class FileEmailTemplateRenderer(IOptions<TemplateOptions> options, IHostEnvironment environment) : IEmailTemplateRenderer
{
    public async Task<RenderedEmail> RenderAsync(string templateId, int version, IReadOnlyDictionary<string, string> variables, CancellationToken ct)
    {
        if (!SafeId().IsMatch(templateId) || version is < 1 or > 10000) throw new TemplateException("Template identifier is invalid.");
        var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.RootPath));
        var folder = Path.GetFullPath(Path.Combine(root, templateId, $"v{version}"));
        if (!folder.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new TemplateException("Template identifier is invalid.");
        var metadataPath = Path.Combine(folder, "subject.txt"); var htmlPath = Path.Combine(folder, "body.html"); var textPath = Path.Combine(folder, "body.txt");
        if (!File.Exists(metadataPath) || !File.Exists(htmlPath) || !File.Exists(textPath)) throw new TemplateException("The requested template version does not exist.");
        var subject = await File.ReadAllTextAsync(metadataPath, ct); var html = await File.ReadAllTextAsync(htmlPath, ct); var text = await File.ReadAllTextAsync(textPath, ct);
        var required = Placeholder().Matches(subject + html + text).Select(x => x.Groups[1].Value).Distinct(StringComparer.Ordinal).ToArray();
        var missing = required.Where(x => !variables.ContainsKey(x)).ToArray();
        if (missing.Length > 0) throw new TemplateException($"Missing required template variables: {string.Join(", ", missing)}.");
        foreach (var key in required)
        {
            var raw = variables[key]; subject = subject.Replace("{{" + key + "}}", raw, StringComparison.Ordinal);
            html = html.Replace("{{" + key + "}}", WebUtility.HtmlEncode(raw), StringComparison.Ordinal);
            text = text.Replace("{{" + key + "}}", raw, StringComparison.Ordinal);
        }
        return new RenderedEmail(subject.Trim(), html, text);
    }
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,99}$")] private static partial Regex SafeId();
    [GeneratedRegex("\\{\\{([a-zA-Z0-9_.-]{1,100})\\}\\}")] private static partial Regex Placeholder();
}
public sealed class TemplateException(string message) : Exception(message);
