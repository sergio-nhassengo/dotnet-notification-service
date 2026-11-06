using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Notifications.Options;

public sealed class TemplateOptions
{
    public const string SectionName = "EmailTemplates";
    [Required] public string RootPath { get; set; } = "Templates";
}
