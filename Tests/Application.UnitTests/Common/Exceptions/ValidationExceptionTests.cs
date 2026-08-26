using FluentValidation.Results;
using ValidationException = Application.Common.Exceptions.ValidationException;

namespace Application.UnitTests.Common.Exceptions;

public class ValidationExceptionTests
{
    [Fact]
    public void Default_ctor_uses_the_standard_message()
    {
        var exception = new ValidationException();

        Assert.Equal("One or more validation failures have occurred.", exception.Message);
        Assert.Empty(exception.Errors);
    }

    [Fact]
    public void Failures_are_grouped_by_property_name()
    {
        var failures = new List<ValidationFailure>
        {
            new("Title", "Title is required."),
            new("Title", "Title is too long."),
            new("Id", "Id is required.")
        };

        var exception = new ValidationException(failures);

        Assert.Equal(2, exception.Errors.Count);
        Assert.Equal(["Title is required.", "Title is too long."], exception.Errors["Title"]);
        Assert.Equal(["Id is required."], exception.Errors["Id"]);
    }
}
