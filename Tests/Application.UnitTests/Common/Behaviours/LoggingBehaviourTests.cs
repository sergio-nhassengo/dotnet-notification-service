using Application.Common.Behaviours;
using Application.Notifications.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Application.UnitTests.Common.Behaviours;

public class LoggingBehaviourTests
{
    public record SampleRequest : IRequest<string>;

    [Fact]
    public async Task Handle_calls_next_and_returns_its_result()
    {
        var logger = new FakeLogger<LoggingBehaviour<SampleRequest, string>>();
        var caller = Substitute.For<INotificationCaller>();
        caller.ApplicationId.Returns("tan-webservice");

        var behaviour = new LoggingBehaviour<SampleRequest, string>(logger, caller);

        var result = await behaviour.Handle(new SampleRequest(), _ => Task.FromResult("response"), CancellationToken.None);

        Assert.Equal("response", result);
    }

    [Fact]
    public async Task Handle_logs_at_information_level()
    {
        var logger = new FakeLogger<LoggingBehaviour<SampleRequest, string>>();
        var caller = Substitute.For<INotificationCaller>();
        caller.ApplicationId.Returns("tan-webservice");

        var behaviour = new LoggingBehaviour<SampleRequest, string>(logger, caller);

        await behaviour.Handle(new SampleRequest(), _ => Task.FromResult("response"), CancellationToken.None);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information);
    }

    [Fact]
    public async Task Handle_falls_back_to_anonymous_when_there_is_no_calling_application()
    {
        var logger = new FakeLogger<LoggingBehaviour<SampleRequest, string>>();
        var caller = Substitute.For<INotificationCaller>();
        caller.ApplicationId.Returns("");

        var behaviour = new LoggingBehaviour<SampleRequest, string>(logger, caller);

        var result = await behaviour.Handle(new SampleRequest(), _ => Task.FromResult("response"), CancellationToken.None);

        Assert.Equal("response", result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("anonymous"));
    }
}
