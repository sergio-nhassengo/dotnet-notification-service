using System.Reflection;
using Application.Common.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Persistence.UnitTests;

public sealed class UniqueConstraintViolationTests
{
    [Theory]
    [InlineData(2601, true)]
    [InlineData(2627, true)]
    [InlineData(515, false)]
    [InlineData(547, false)]
    public void Sql_server_classifies_only_duplicate_numbers(int number, bool expected)
    {
        var constructor = typeof(SqlError).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .First(c => c.GetParameters().Length == 8);
        var error = (SqlError)constructor.Invoke([number, (byte)0, (byte)0, "server", "test", "procedure", 1, null]);
        var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(errors, [error]);
        var factory = typeof(SqlException).GetMethod("CreateException", BindingFlags.Static | BindingFlags.NonPublic,
            null, [typeof(SqlErrorCollection), typeof(string)], null)!;
        var exception = (SqlException)factory.Invoke(null, [errors, "16.0"])!;
        Assert.Equal(expected, UniqueConstraintViolation.IsExpected(new DbUpdateException("save", exception)));
    }

    [Theory]
    [InlineData(2067, true)] // UNIQUE
    [InlineData(1555, true)] // PRIMARY KEY
    [InlineData(1299, false)] // NOT NULL
    [InlineData(787, false)] // FOREIGN KEY
    [InlineData(275, false)] // CHECK
    [InlineData(19, false)] // No extended classification
    public void Sqlite_uses_extended_constraint_codes(int extendedCode, bool expected) =>
        Assert.Equal(expected, UniqueConstraintViolation.IsExpected(
            new DbUpdateException("save", new SqliteException("test", 19, extendedCode))));

    [Fact]
    public void Unrelated_update_failure_is_not_a_duplicate() =>
        Assert.False(UniqueConstraintViolation.IsExpected(new DbUpdateException("save", new IOException("disk"))));
}
