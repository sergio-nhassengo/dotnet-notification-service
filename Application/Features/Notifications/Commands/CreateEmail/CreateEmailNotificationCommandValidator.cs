using System.Net.Mail;
using System.Text.Json;
using FluentValidation;

namespace Application.Features.Notifications.Commands.CreateEmail;

public sealed class CreateEmailNotificationCommandValidator : AbstractValidator<CreateEmailNotificationCommand>
{
    public CreateEmailNotificationCommandValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CorrelationId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Subject).MaximumLength(200).Must(x => x is null || !x.Contains('\r') && !x.Contains('\n'));
        RuleFor(x => x)
            .Must(x => x.To is not null && (x.Recipient is null ? x.To.Count > 0 : x.To.Count == 0))
            .WithMessage("Provide either recipient or a nonempty to collection.");
        RuleFor(x => x.To).NotNull(); 
        RuleFor(x => x.Cc).NotNull(); 
        RuleFor(x => x).Custom((x, ctx) =>
        {
            var recipients = (x.To ?? []).Concat(x.Cc ?? []).ToList();
            if (x.Recipient is not null) 
                recipients.Add(new(x.Recipient.Email, x.Recipient.Name));
            if (recipients.Any(r => r is null || !ValidEmail(r.Email) || r.Name?.Length > 200 || r.Name?.IndexOfAny(['\r', '\n']) >= 0))
                ctx.AddFailure("Recipients", "Recipients must contain individual valid email addresses and valid display names.");
            if (recipients.Count > 100) ctx.AddFailure("Recipients", "At most 100 recipients are supported.");
            var raw = x.HtmlBody is not null || x.TextBody is not null;
            if (raw)
            {
                if (!string.IsNullOrEmpty(x.TemplateId) || x.Variables?.Count > 0)
                    ctx.AddFailure("TemplateId", "Raw content and templates cannot be combined.");
                if (string.IsNullOrWhiteSpace(x.Subject) || string.IsNullOrWhiteSpace(x.HtmlBody) && string.IsNullOrWhiteSpace(x.TextBody))
                    ctx.AddFailure("Content", "Raw content requires a subject and a nonempty HTML or text body.");
            }
            else if (x.TemplateId is null || !System.Text.RegularExpressions.Regex.IsMatch(x.TemplateId, "^[a-z0-9][a-z0-9-]{0,99}$"))
                ctx.AddFailure("TemplateId", "A valid template ID is required.");
            if ((x.HtmlBody?.Length ?? 0) + (x.TextBody?.Length ?? 0) > 100000)
                ctx.AddFailure("Content", "Email bodies must total at most 100000 characters.");
            if (x.Variables is not null && (x.Variables.Count > 50 || x.Variables.Any(v => v.Value is null || v.Key.Length > 100) || JsonSerializer.Serialize(x.Variables).Length > 20000))
                ctx.AddFailure("Variables", "Template variables exceed the supported limits or contain null values.");
        });
    }
    public static bool ValidEmail(string? value) => value is not null && value.Length <= 320 &&
        !value.Contains('\r') && !value.Contains('\n') && MailAddress.TryCreate(value, out var address) && address.Address == value;
}
