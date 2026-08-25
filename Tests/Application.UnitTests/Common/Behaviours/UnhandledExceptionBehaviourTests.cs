using Application.Common.Behaviours;
using Application.UnitTests.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UnitTests.Common.Behaviours;

public class UnhandledExceptionBehaviourTests
{
    public record SampleRequest : IRequest<string>;

    [Fact]
    public async Task A_generic_exception_is_logged_and_rethrown()
    {
        var logger = new FakeLogger<UnhandledExceptionBehaviour<SampleRequest, string>>();
        var behaviour = new UnhandledExceptionBehaviour<SampleRequest, string>(logger);
        var thrown = new InvalidOperationException("boom");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behaviour.Handle(new SampleRequest(), _ => throw thrown, CancellationToken.None));

        Assert.Same(thrown, exception);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Exception == thrown);
    }

    [Fact]
    public async Task A_ValidationException_is_not_logged_and_still_propagates()
    {
        var logger = new FakeLogger<UnhandledExceptionBehaviour<SampleRequest, string>>();
        var behaviour = new UnhandledExceptionBehaviour<SampleRequest, string>(logger);
        var thrown = new Application.Common.Exceptions.ValidationException();

        var exception = await Assert.ThrowsAsync<Application.Common.Exceptions.ValidationException>(() =>
            behaviour.Handle(new SampleRequest(), _ => throw thrown, CancellationToken.None));

        Assert.Same(thrown, exception);
        Assert.Empty(logger.Entries);
    }
}
