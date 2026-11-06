namespace Application.Common.Behaviours;

/// <summary>
/// Marks a request whose normal duration includes external I/O (for example an email provider call),
/// so <see cref="PerformanceBehaviour{TRequest,TResponse}"/> does not report it as slow.
/// </summary>
public interface ILongRunningRequest;
