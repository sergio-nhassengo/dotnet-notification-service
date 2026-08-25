using Domain.Common;

namespace Domain.UnitTests;

public class BaseAuditableEntityTests
{
    private sealed class TestAuditableEntity : BaseAuditableEntity<int>;

    [Fact]
    public void Created_defaults_to_now()
    {
        var before = DateTimeOffset.UtcNow;

        var entity = new TestAuditableEntity();

        var after = DateTimeOffset.UtcNow;
        Assert.InRange(entity.Created, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public void LastModified_defaults_to_now()
    {
        var before = DateTimeOffset.UtcNow;

        var entity = new TestAuditableEntity();

        var after = DateTimeOffset.UtcNow;
        Assert.InRange(entity.LastModified, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public void CreatedBy_and_LastModifiedBy_default_to_null()
    {
        var entity = new TestAuditableEntity();

        Assert.Null(entity.CreatedBy);
        Assert.Null(entity.LastModifiedBy);
    }
}
