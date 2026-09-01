using Application.Common.Behaviours;
using Domain.Common;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Application.UnitTests.Common.Behaviours;

public class ValidationBehaviourTests
{
    public record SampleRequest : IRequest<Result<string>>;

    private sealed class PassingValidator : AbstractValidator<SampleRequest>;

    private sealed class FailingValidator : AbstractValidator<SampleRequest>
    {
        public FailingValidator() => RuleFor(r => r).Custom((_, context) =>
            context.AddFailure(new ValidationFailure("Field", "Failed.")));
    }

    [Fact]
    public async Task No_validators_calls_next_directly()
    {
        var behaviour = new ValidationBehaviour<SampleRequest, Result<string>>([]);

        var result = await behaviour.Handle(new SampleRequest(), _ => Task.FromResult(Result.Success("response")), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("response", result.Value);
    }

    [Fact]
    public async Task Failing_validators_return_a_validation_failure_and_never_call_next()
    {
        var behaviour = new ValidationBehaviour<SampleRequest, Result<string>>([new FailingValidator()]);
        var nextCalled = false;

        var result = await behaviour.Handle(new SampleRequest(), _ =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success("response"));
        }, CancellationToken.None);

        Assert.False(nextCalled);
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        var validationError = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(validationError.Errors, e => e.Code == "Field" && e.Message == "Failed.");
    }

    [Fact]
    public async Task Passing_validators_call_next()
    {
        var behaviour = new ValidationBehaviour<SampleRequest, Result<string>>([new PassingValidator()]);

        var result = await behaviour.Handle(new SampleRequest(), _ => Task.FromResult(Result.Success("response")), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("response", result.Value);
    }
}
