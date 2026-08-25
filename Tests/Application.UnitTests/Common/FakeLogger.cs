using Microsoft.Extensions.Logging;

namespace Application.UnitTests.Common;

// NSubstitute can't reliably verify calls made through the LogInformation/LogWarning/LogError
// extension methods, because their runtime TState type won't match a compile-time Arg.Any<T>()
// used in a verification expression. A hand-rolled fake avoids that mismatch entirely.
internal sealed class FakeLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception), exception));
}
