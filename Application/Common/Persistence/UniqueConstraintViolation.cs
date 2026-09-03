using Microsoft.EntityFrameworkCore;

namespace Application.Common.Persistence;

internal static class UniqueConstraintViolation
{
    public static bool IsExpected(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            var typeName = current.GetType().FullName;
            if (typeName == "Microsoft.Data.SqlClient.SqlException" &&
                current.GetType().GetProperty("Number")?.GetValue(current) is int number &&
                number is 2601 or 2627)
                return true;

            if (typeName == "Microsoft.Data.Sqlite.SqliteException" &&
                current.GetType().GetProperty("SqliteErrorCode")?.GetValue(current) is int sqliteCode &&
                sqliteCode == 19)
                return true;
        }

        return false;
    }
}
