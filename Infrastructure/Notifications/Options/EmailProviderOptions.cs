using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Notifications.Options;

public sealed class EmailProviderOptions
{
    public const string SectionName = "EmailProvider";
    [Required] public string Provider { get; set; } = "SmtpRelay";
}
