using Application.Common.Interfaces;

namespace Persistence.UnitTests;

internal sealed class FakeDateTime(DateTimeOffset now) : IDateTime
{
    public DateTimeOffset Now { get; set; } = now;
}
