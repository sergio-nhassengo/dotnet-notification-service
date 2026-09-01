using Domain.Common;

namespace Domain.UnitTests;

public class ResultTests
{
    [Fact]
    public void Success_produces_a_result_with_no_error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_carries_the_given_error()
    {
        var error = Error.NotFound("Todo.NotFound", "Not found.");

        var result = Result.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Generic_success_exposes_the_value()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Generic_failure_throws_when_the_value_is_accessed()
    {
        var result = Result.Failure<int>(Error.Conflict("Code", "Message"));

        Assert.True(result.IsFailure);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void A_value_implicitly_converts_to_a_successful_result()
    {
        Result<string> result = "hello";

        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void EntityNotFound_formats_the_message_like_the_legacy_NotFoundException()
    {
        var error = Error.EntityNotFound("TodoList", 42);

        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal("Entity \"TodoList\" (42) was not found.", error.Message);
    }
}
