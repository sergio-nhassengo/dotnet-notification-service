using Domain.Events;

namespace Domain.UnitTests;

public class DomainEventTests
{
    private sealed class TestDomainEvent : DomainEvent;

    [Fact]
    public void OccurredOn_is_set_to_now_on_construction()
    {
        var before = DateTimeOffset.UtcNow;

        var domainEvent = new TestDomainEvent();

        var after = DateTimeOffset.UtcNow;
        Assert.InRange(domainEvent.OccurredOn, before.AddSeconds(-1), after.AddSeconds(1));
    }
}
