using System.Net.Mail;
using System.Net.Mime;
using System.Net;
using Domain.Enums;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Options;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 25;
    public bool EnableSsl { get; set; } = true;
    public bool UseDefaultCredentials { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
}
