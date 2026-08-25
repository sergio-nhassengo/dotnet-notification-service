using Domain.Common;
using Domain.Events;

namespace Domain.UnitTests;

public class BaseEntityTests
{
    private sealed class TestDomainEvent : DomainEvent;

    private sealed class TestEntity : BaseEntity<int>;

    [Fact]
    public void AddDomainEvent_adds_the_event_to_DomainEvents()
    {
        var entity = new TestEntity();
        var domainEvent = new TestDomainEvent();

        entity.AddDomainEvent(domainEvent);

        Assert.Contains(domainEvent, entity.DomainEvents);
    }

    [Fact]
    public void RemoveDomainEvent_removes_only_the_specified_event()
    {
        var entity = new TestEntity();
        var first = new TestDomainEvent();
        var second = new TestDomainEvent();

        entity.AddDomainEvent(first);
        entity.AddDomainEvent(second);

        entity.RemoveDomainEvent(first);

        Assert.DoesNotContain(first, entity.DomainEvents);
        Assert.Contains(second, entity.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_empties_the_collection()
    {
        var entity = new TestEntity();
        entity.AddDomainEvent(new TestDomainEvent());
        entity.AddDomainEvent(new TestDomainEvent());

        entity.ClearDomainEvents();

        Assert.Empty(entity.DomainEvents);
    }

    [Fact]
    public void DomainEvents_is_exposed_as_a_read_only_collection()
    {
        var entity = new TestEntity();

        Assert.IsAssignableFrom<IReadOnlyCollection<DomainEvent>>(entity.DomainEvents);
    }
}
