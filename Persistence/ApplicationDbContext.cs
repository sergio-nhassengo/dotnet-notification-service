using System.Reflection;
using Application.Common.Interfaces;
using Application.Common.Security;
using Domain.Common;
using Domain.Entities;
using Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace Persistence;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IDateTime dateTime,
    ICurrentUserService currentUserService)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<TodoList> TodoLists => Set<TodoList>();

    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // BaseEntity exposes DomainEvents for in-memory dispatch only - never persisted.
        builder.Ignore<DomainEvent>();

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

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

        foreach (var entry in ChangeTracker.Entries<BaseAuditableEntity>())
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
