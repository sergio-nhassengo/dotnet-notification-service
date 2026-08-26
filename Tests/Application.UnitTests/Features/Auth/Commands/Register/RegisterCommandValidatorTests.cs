using Application.Features.Auth.Commands.Register;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Features.Auth.Commands.Register;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    private static RegisterCommand ValidCommand() =>
        new("jane@example.com", "Jane", "Doe", "jane", "1234567890", "password123");

    [Fact]
    public void Invalid_email_fails()
    {
        var command = ValidCommand() with { Email = "not-an-email" };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Fact]
    public void Empty_email_fails()
    {
        var command = ValidCommand() with { Email = "" };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Fact]
    public void Empty_first_name_fails()
    {
        var command = ValidCommand() with { FirstName = "" };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.FirstName);
    }

    [Fact]
    public void Empty_last_name_fails()
    {
        var command = ValidCommand() with { LastName = "" };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.LastName);
    }

    [Fact]
    public void Empty_username_fails()
    {
        var command = ValidCommand() with { UserName = "" };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.UserName);
    }

    [Fact]
    public void Mobile_phone_over_20_characters_fails()
    {
        var command = ValidCommand() with { MobilePhone = new string('1', 21) };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.MobilePhone);
    }

    [Fact]
    public void Password_under_8_characters_fails()
    {
        var command = ValidCommand() with { Password = "short1" };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Password);
    }

    [Fact]
    public void Fully_valid_command_passes()
    {
        var result = _validator.TestValidate(ValidCommand());

        result.ShouldNotHaveAnyValidationErrors();
    }
}
