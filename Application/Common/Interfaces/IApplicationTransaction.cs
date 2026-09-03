using System.Data;

namespace Application.Common.Interfaces;

public interface IApplicationTransaction
{
    Task<T> ExecuteAsync<T>(IsolationLevel isolationLevel,
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}
