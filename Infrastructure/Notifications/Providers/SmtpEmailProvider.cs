using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Application.Common.Email;
using Application.Notifications.Integration;
using Application.Notifications.Models;
using Domain.Enums;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Providers;

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
public sealed class SmtpEmailProvider(IOptions<SmtpOptions> options) : IEmailProvider
{
    public string Name => "Smtp";
    public async Task<EmailProviderResult> SendAsync(EmailMessage email, CancellationToken ct)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.Host) || o.Port is < 1 or > 65535 || o.TimeoutSeconds is < 1 or > 120)
            return EmailProviderResult.Failure(EmailFailureCategory.Configuration, "Smtp.Configuration", "SMTP host, port or timeout is invalid.");
        using var client = new SmtpClient(o.Host, o.Port) { EnableSsl = o.EnableSsl, UseDefaultCredentials = o.UseDefaultCredentials };
        if (!string.IsNullOrEmpty(o.Username)) 
            client.Credentials = new NetworkCredential(o.Username, o.Password);
        using var message = CreateMessage(email);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(o.TimeoutSeconds));
        try
        {
            await client.SendMailAsync(message, timeout.Token);
            return EmailProviderResult.Success(email.MessageId.ToString());
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return EmailProviderResult.Failure(EmailFailureCategory.Transient, "Smtp.Timeout", "SMTP delivery timed out.");
        }
        catch (SmtpException ex)
        {
            // A partial recipient failure may have delivered to other recipients. Do not automatically resend the group.
            var category = ex is SmtpFailedRecipientException ? EmailFailureCategory.Permanent :
                (int)ex.StatusCode >= 500 ? EmailFailureCategory.Permanent : EmailFailureCategory.Transient;
            return EmailProviderResult.Failure(category, ex is SmtpFailedRecipientException ? "Smtp.RecipientFailure" : "Smtp.Rejected",
                "SMTP delivery failed; a recipient failure may represent partial delivery.", (int)ex.StatusCode);
        }
    }
    public static MailMessage CreateMessage(EmailMessage email)
    {
        var message = new MailMessage { From = new MailAddress(email.SenderEmail, email.SenderName), Subject = email.Subject };
        var to = email.To.Count > 0 ? email.To : new[] { new EmailAddress(email.RecipientEmail, email.RecipientName) };
        foreach (var recipient in to) 
            message.To.Add(new MailAddress(recipient.Email, recipient.Name));
        foreach (var recipient in email.Cc) 
            message.CC.Add(new MailAddress(recipient.Email, recipient.Name));
        if (email.ReplyTo is not null) 
            message.ReplyToList.Add(email.ReplyTo);
        message.Headers.Add("X-Notification-Id", email.MessageId.ToString());
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(email.TextBody, System.Text.Encoding.UTF8, MediaTypeNames.Text.Plain));
        var html = AlternateView.CreateAlternateViewFromString(email.HtmlBody, System.Text.Encoding.UTF8, MediaTypeNames.Text.Html);
        message.AlternateViews.Add(html);
        foreach (var file in email.Attachments)
        {
            if (file.ContentId is not null)
                html.LinkedResources.Add(new LinkedResource(new MemoryStream(file.Content), file.ContentType) { ContentId = file.ContentId, TransferEncoding = TransferEncoding.Base64 });
            else message.Attachments.Add(new Attachment(new MemoryStream(file.Content), file.FileName, file.ContentType));
        }
        return message;
    }
}
