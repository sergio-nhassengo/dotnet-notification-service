using Application.Common.Interfaces;

namespace Application.UnitTests.Common;

internal sealed class FakeDateTime(DateTimeOffset now) : IDateTime
{
    public DateTimeOffset Now { get; set; } = now;
}
