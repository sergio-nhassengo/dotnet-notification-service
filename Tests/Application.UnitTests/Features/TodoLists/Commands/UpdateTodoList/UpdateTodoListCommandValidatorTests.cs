using Application.Features.TodoLists.Commands.UpdateTodoList;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Features.TodoLists.Commands.UpdateTodoList;

public class UpdateTodoListCommandValidatorTests
{
    private readonly UpdateTodoListCommandValidator _validator = new();

    [Fact]
    public void Empty_id_fails()
    {
        var result = _validator.TestValidate(new UpdateTodoListCommand(0, "Groceries"));

        result.ShouldHaveValidationErrorFor(c => c.Id);
    }

    [Fact]
    public void Empty_title_fails()
    {
        var result = _validator.TestValidate(new UpdateTodoListCommand(1, ""));

        result.ShouldHaveValidationErrorFor(c => c.Title);
    }

    [Fact]
    public void Title_over_200_characters_fails()
    {
        var result = _validator.TestValidate(new UpdateTodoListCommand(1, new string('a', 201)));

        result.ShouldHaveValidationErrorFor(c => c.Title);
    }

    [Fact]
    public void Valid_command_passes()
    {
        var result = _validator.TestValidate(new UpdateTodoListCommand(1, "Groceries"));

        result.ShouldNotHaveValidationErrorFor(c => c.Id);
        result.ShouldNotHaveValidationErrorFor(c => c.Title);
    }
}
