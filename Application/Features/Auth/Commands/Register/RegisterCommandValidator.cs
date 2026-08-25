using FluentValidation;

namespace Application.Features.Auth.Commands.Register;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(v => v.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(v => v.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(v => v.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(v => v.UserName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(v => v.MobilePhone)
            .MaximumLength(20);

        RuleFor(v => v.Password)
            .NotEmpty()
            .MinimumLength(8);
    }
}
