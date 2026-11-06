using System.Reflection;
using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<NotificationAttachment> NotificationAttachments => Set<NotificationAttachment>();

    public DbSet<EmailNotification> EmailNotifications => Set<EmailNotification>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // SQLite cannot translate comparisons or ordering for DateTimeOffset values.
        // Keep the existing binary representation locally so due-time queries remain server-side.
        if (Database.IsSqlite())
        {
            var converter = new DateTimeOffsetToBinaryConverter();

            foreach (var property in builder.Model.GetEntityTypes()
                         .SelectMany(entityType => entityType.GetProperties())
                         .Where(property => property.ClrType == typeof(DateTimeOffset) ||
                                            property.ClrType == typeof(DateTimeOffset?)))
            {
                property.SetValueConverter(converter);
            }
        }

        base.OnModelCreating(builder);
    }

    public void ClearTrackedChanges() => ChangeTracker.Clear();
}
