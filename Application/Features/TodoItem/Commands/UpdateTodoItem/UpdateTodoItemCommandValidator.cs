using FluentValidation;

namespace Application.Features.TodoItem.Commands.UpdateTodoItem;

public class UpdateTodoItemCommandValidator : AbstractValidator<UpdateTodoItemCommand>
{
    public UpdateTodoItemCommandValidator()
    {
        RuleFor(v => v.Id)
                    .GreaterThan(0);

        RuleFor(v => v.Title)
                    .NotEmpty()
                    .MaximumLength(200);
    }
}
