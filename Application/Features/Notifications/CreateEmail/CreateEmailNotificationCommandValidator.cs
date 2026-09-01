using FluentValidation;

namespace Application.Features.Notifications.Commands.CreateEmail;

public sealed class CreateEmailNotificationCommandValidator : AbstractValidator<CreateEmailNotificationCommand>
{
    public CreateEmailNotificationCommandValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CorrelationId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Recipient).NotNull();
        RuleFor(x => x.Recipient.Email).NotEmpty().EmailAddress().MaximumLength(320).When(x => x.Recipient is not null);
        RuleFor(x => x.Recipient.Name).MaximumLength(200).When(x => x.Recipient is not null);
        RuleFor(x => x.TemplateId).NotEmpty().Matches("^[a-z0-9][a-z0-9-]{0,99}$");
        RuleFor(x => x.TemplateVersion).InclusiveBetween(1, 10000);
        RuleFor(x => x.Subject).MaximumLength(200);
        RuleFor(x => x.Priority).Must(x => Enum.TryParse<Domain.Enums.NotificationPriority>(x, true, out _))
            .WithMessage("Priority must be Low, Normal, or High.");
        RuleFor(x => x.ScheduledAt).Must(x => x is null || x < DateTimeOffset.UtcNow.AddYears(1))
            .WithMessage("ScheduledAt must be less than one year in the future.");
        RuleFor(x => x.Variables).NotNull().Must(x => x.Count <= 50).WithMessage("At most 50 variables are allowed.");
        RuleForEach(x => x.Variables).Must(x => x.Key.Length is > 0 and <= 100 && x.Value.Length <= 4000)
            .WithMessage("Variable names and values exceed allowed lengths.");
    }
}
