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
    public async Task No_exception_type_is_exempt_from_logging()
    {
        var logger = new FakeLogger<UnhandledExceptionBehaviour<SampleRequest, string>>();
        var behaviour = new UnhandledExceptionBehaviour<SampleRequest, string>(logger);
        var thrown = new ApplicationException("boom");

        var exception = await Assert.ThrowsAsync<ApplicationException>(() =>
            behaviour.Handle(new SampleRequest(), _ => throw thrown, CancellationToken.None));

        Assert.Same(thrown, exception);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Exception == thrown);
    }
}
