using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Application.Common.Security;
using Domain.Common;
using Domain.Entities;
using Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Persistence;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IDateTime dateTime,
    ICurrentUserService currentUserService)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();
    
    public DbSet<EmailNotification> EmailNotifications => Set<EmailNotification>();
    public DbSet<DeliveryAttempt> DeliveryAttempts => Set<DeliveryAttempt>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<NotificationReplay> NotificationReplays => Set<NotificationReplay>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // BaseEntity exposes DomainEvents for in-memory dispatch only - never persisted.
        builder.Ignore<DomainEvent>();

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // SQLite cannot translate comparisons or ordering for DateTimeOffset values.
        // Store them as UTC ticks locally so worker lease/scheduling queries remain server-side.
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

            // SQL Server generates rowversion values; SQLite does not. Keep the
            // concurrency token but persist the entity's byte array value locally.
            foreach (var property in builder.Model.GetEntityTypes()
                         .SelectMany(entityType => entityType.GetProperties())
                         .Where(property => property.ClrType == typeof(byte[]) && property.IsConcurrencyToken))
            {
                property.ValueGenerated = ValueGenerated.Never;
            }
        }

        base.OnModelCreating(builder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditableEntities();

        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        UpdateAuditableEntities();

        return base.SaveChanges();
    }

    private void UpdateAuditableEntities()
    {
        var now = dateTime.Now;
        var userId = currentUserService.UserId ?? "anonymous";

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.Created = now;
                    entry.Entity.CreatedBy = userId;
                    entry.Entity.LastModified = now;
                    entry.Entity.LastModifiedBy = userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.LastModified = now;
                    entry.Entity.LastModifiedBy = userId;
                    break;
            }
        }
    }
}
