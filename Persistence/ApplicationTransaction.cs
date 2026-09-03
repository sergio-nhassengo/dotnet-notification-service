using System.Data;
using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Persistence;

public sealed class ApplicationTransaction(ApplicationDbContext db) : IApplicationTransaction
{
    public async Task<T> ExecuteAsync<T>(IsolationLevel isolationLevel,
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        try
        {
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // Preserve the operation's original exception.
            }
            throw;
        }
    }
}
