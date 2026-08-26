using Application.Common.Behaviours;
using Application.Common.Security;
using Application.UnitTests.Common;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Application.UnitTests.Common.Behaviours;

public class PerformanceBehaviourTests
{
    public record SampleRequest : IRequest<string>;

    private static (PerformanceBehaviour<SampleRequest, string> Behaviour, FakeLogger<PerformanceBehaviour<SampleRequest, string>> Logger) CreateBehaviour()
    {
        var logger = new FakeLogger<PerformanceBehaviour<SampleRequest, string>>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("42");

        return (new PerformanceBehaviour<SampleRequest, string>(logger, currentUserService), logger);
    }

    [Fact]
    public async Task Handle_does_not_log_a_warning_for_a_fast_request()
    {
        var (behaviour, logger) = CreateBehaviour();

        var result = await behaviour.Handle(new SampleRequest(), _ => Task.FromResult("response"), CancellationToken.None);

        Assert.Equal("response", result);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task Handle_logs_a_warning_for_a_slow_request()
    {
        var (behaviour, logger) = CreateBehaviour();

        await behaviour.Handle(new SampleRequest(), async _ =>
        {
            await Task.Delay(600);
            return "response";
        }, CancellationToken.None);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }
}
