using Application.Features.TodoLists.Commands.CreateTodoList;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Features.TodoLists.Commands.CreateTodoList;

public class CreateTodoListCommandValidatorTests
{
    private readonly CreateTodoListCommandValidator _validator = new();

    [Fact]
    public void Empty_title_fails()
    {
        var result = _validator.TestValidate(new CreateTodoListCommand(""));

        result.ShouldHaveValidationErrorFor(c => c.Title);
    }

    [Fact]
    public void Title_over_200_characters_fails()
    {
        var result = _validator.TestValidate(new CreateTodoListCommand(new string('a', 201)));

        result.ShouldHaveValidationErrorFor(c => c.Title);
    }

    [Fact]
    public void Valid_title_passes()
    {
        var result = _validator.TestValidate(new CreateTodoListCommand("Groceries"));

        result.ShouldNotHaveValidationErrorFor(c => c.Title);
    }
}
