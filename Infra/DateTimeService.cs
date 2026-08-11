using Application.Common.Interfaces;

namespace Infra;

public class DateTimeService : IDateTime
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}
