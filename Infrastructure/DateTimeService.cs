using System;
using Application.Common.Interfaces;

namespace Infrastructure;

public class DateTimeService : IDateTime
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}
