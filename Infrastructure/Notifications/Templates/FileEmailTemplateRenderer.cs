using System.Net;
using System.Text.RegularExpressions;
using Application.Notifications.Abstractions;
using Application.Notifications.Models;
using Infrastructure.Notifications.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Templates;

public sealed partial class FileEmailTemplateRenderer(IOptions<TemplateOptions> options, IHostEnvironment environment) : Application.Notifications.Abstractions.IEmailTemplateRenderer
{
    public async Task<RenderedEmail> RenderAsync(string templateId, IReadOnlyDictionary<string, string> variables, CancellationToken ct)
    {
        if (!SafeId().IsMatch(templateId))
            throw new TemplateException("Template identifier is invalid.");
        
        var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.RootPath));
        var folder = Path.GetFullPath(Path.Combine(root, templateId));
        
        if (!folder.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new TemplateException("Template identifier is invalid.");
        
        var metadataPath = Path.Combine(folder, "subject.txt");
        var htmlPath = Path.Combine(folder, "body.html");
        var textPath = Path.Combine(folder, "body.txt");
        
        if (!File.Exists(metadataPath) || !File.Exists(htmlPath) || !File.Exists(textPath))
            throw new TemplateException("The requested template does not exist.");
        
        var subject = await File.ReadAllTextAsync(metadataPath, ct);
        var html = await File.ReadAllTextAsync(htmlPath, ct);
        var text = await File.ReadAllTextAsync(textPath, ct);
        var required = Placeholder()
            .Matches(subject + html + text).
            Select(x => x.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var missing = required.Where(x => !variables.TryGetValue(x, out var value) || string.IsNullOrWhiteSpace(value)).ToArray();
        
        if (missing.Length > 0)
            throw new TemplateException($"Missing or empty required template variables: {string.Join(", ", missing)}.");
        
        // Replace only placeholders in the original template. Values must never become executable template syntax.
        subject = Placeholder().Replace(subject, match => variables[match.Groups[1].Value]);
        html = Placeholder().Replace(html, match => WebUtility.HtmlEncode(variables[match.Groups[1].Value]));
        text = Placeholder().Replace(text, match => variables[match.Groups[1].Value]);
        return new RenderedEmail(subject.Trim(), html, text);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,99}$")]
    private static partial Regex SafeId();
    [GeneratedRegex("\\{\\{([a-zA-Z0-9_.-]{1,100})\\}\\}")]
    private static partial Regex Placeholder();
}
