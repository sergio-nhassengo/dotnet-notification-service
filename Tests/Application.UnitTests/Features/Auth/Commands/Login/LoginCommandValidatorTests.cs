using Application.Features.Auth.Commands.Login;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Features.Auth.Commands.Login;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Empty_username_fails()
    {
        var result = _validator.TestValidate(new LoginCommand("", "password"));

        result.ShouldHaveValidationErrorFor(c => c.UserName);
    }

    [Fact]
    public void Empty_password_fails()
    {
        var result = _validator.TestValidate(new LoginCommand("jane", ""));

        result.ShouldHaveValidationErrorFor(c => c.Password);
    }

    [Fact]
    public void Both_present_passes()
    {
        var result = _validator.TestValidate(new LoginCommand("jane", "password"));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
