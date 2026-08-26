using Application.Common.Behaviours;
using Application.Common.Security;
using Application.UnitTests.Common;
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
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("42");

        var behaviour = new LoggingBehaviour<SampleRequest, string>(logger, currentUserService);

        var result = await behaviour.Handle(new SampleRequest(), _ => Task.FromResult("response"), CancellationToken.None);

        Assert.Equal("response", result);
    }

    [Fact]
    public async Task Handle_logs_at_information_level()
    {
        var logger = new FakeLogger<LoggingBehaviour<SampleRequest, string>>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("42");

        var behaviour = new LoggingBehaviour<SampleRequest, string>(logger, currentUserService);

        await behaviour.Handle(new SampleRequest(), _ => Task.FromResult("response"), CancellationToken.None);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information);
    }

    [Fact]
    public async Task Handle_falls_back_to_anonymous_when_there_is_no_current_user()
    {
        var logger = new FakeLogger<LoggingBehaviour<SampleRequest, string>>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns((string?)null);

        var behaviour = new LoggingBehaviour<SampleRequest, string>(logger, currentUserService);

        var result = await behaviour.Handle(new SampleRequest(), _ => Task.FromResult("response"), CancellationToken.None);

        Assert.Equal("response", result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("anonymous"));
    }
}
