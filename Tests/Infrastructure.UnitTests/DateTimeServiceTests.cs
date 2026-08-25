using Infrastructure;

namespace Infrastructure.UnitTests;

public class DateTimeServiceTests
{
    [Fact]
    public void Now_returns_a_value_close_to_the_current_UTC_time()
    {
        var service = new DateTimeService();

        var before = DateTimeOffset.UtcNow;
        var now = service.Now;
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(now, before.AddSeconds(-1), after.AddSeconds(1));
    }
}
