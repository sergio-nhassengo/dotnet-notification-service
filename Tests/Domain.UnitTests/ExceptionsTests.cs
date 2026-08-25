using Domain.Exceptions;

namespace Domain.UnitTests;

public class ExceptionsTests
{
    [Fact]
    public void NotFoundException_default_ctor_produces_a_non_empty_message()
    {
        var exception = new NotFoundException();

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    [Fact]
    public void NotFoundException_message_ctor_uses_the_given_message()
    {
        var exception = new NotFoundException("Something was not found.");

        Assert.Equal("Something was not found.", exception.Message);
    }

    [Fact]
    public void NotFoundException_name_and_key_ctor_formats_the_message()
    {
        var exception = new NotFoundException("TodoList", 42);

        Assert.Equal("Entity \"TodoList\" (42) was not found.", exception.Message);
    }

    [Fact]
    public void AuthenticationException_carries_the_given_message()
    {
        var exception = new AuthenticationException("Invalid username or password.");

        Assert.Equal("Invalid username or password.", exception.Message);
    }
}
