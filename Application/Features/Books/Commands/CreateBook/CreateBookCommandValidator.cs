using FluentValidation;

namespace Application.Features.Books.Commands.CreateBook;

public class CreateBookCommandValidator : AbstractValidator<CreateBookCommand>
{
    public CreateBookCommandValidator()
    {
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
