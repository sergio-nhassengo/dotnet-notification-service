using FluentValidation;

namespace Application.Features.Books.Commands.UpdateBook;

public class UpdateBookCommandValidator : AbstractValidator<UpdateBookCommand>
{
    public UpdateBookCommandValidator()
    {
        RuleFor(v => v.Id)
                    .GreaterThan(0);

        RuleFor(v => v.Name)
                    .NotEmpty()
                    .MaximumLength(200);

        RuleFor(v => v.Description)
                    .NotEmpty()
                    .MaximumLength(200);

        RuleFor(v => v.AuthorName)
                    .NotEmpty()
                    .MaximumLength(200);

        RuleFor(v => v.AuthorEmail)
                    .NotEmpty()
                    .MaximumLength(200);
    }
}
