using Application.Common.Behaviours;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using ValidationException = Application.Common.Exceptions.ValidationException;

namespace Application.UnitTests.Common.Behaviours;

public class ValidationBehaviourTests
{
    public record SampleRequest : IRequest<string>;

    private sealed class PassingValidator : AbstractValidator<SampleRequest>;

    private sealed class FailingValidator : AbstractValidator<SampleRequest>
    {
        public FailingValidator() => RuleFor(r => r).Custom((_, context) =>
            context.AddFailure(new ValidationFailure("Field", "Failed.")));
    }

    [Fact]
    public async Task No_validators_calls_next_directly()
    {
        var behaviour = new ValidationBehaviour<SampleRequest, string>([]);

        var result = await behaviour.Handle(new SampleRequest(), _ => Task.FromResult("response"), CancellationToken.None);

        Assert.Equal("response", result);
    }

    [Fact]
    public async Task Failing_validators_throw_and_never_call_next()
    {
        var behaviour = new ValidationBehaviour<SampleRequest, string>([new FailingValidator()]);
        var nextCalled = false;

        await Assert.ThrowsAsync<ValidationException>(() =>
            behaviour.Handle(new SampleRequest(), _ =>
            {
                nextCalled = true;
                return Task.FromResult("response");
            }, CancellationToken.None));

        Assert.False(nextCalled);
    }

    [Fact]
    public async Task Passing_validators_call_next()
    {
        var behaviour = new ValidationBehaviour<SampleRequest, string>([new PassingValidator()]);

        var result = await behaviour.Handle(new SampleRequest(), _ => Task.FromResult("response"), CancellationToken.None);

        Assert.Equal("response", result);
    }
}
