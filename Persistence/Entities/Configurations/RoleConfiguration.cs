using System;
using Domain.Constants;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Entities.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    // Fixed instant for seeded rows - Role.Created/LastModified default to DateTimeOffset.UtcNow via
    // BaseAuditableEntity, and HasData bakes whatever value is set at model-build time into the migration.
    // Leaving that default in place makes the model non-deterministic (a different value every build) and
    // EF refuses to migrate over it, so the seed rows must set an explicit, static value instead.
    private static readonly DateTimeOffset SeedDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.Property(r => r.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.Description)
            .HasMaxLength(500);

        builder.HasIndex(r => r.Name)
            .IsUnique();

        builder.HasData(
            new Role { Id = 1, Name = RoleNames.Admin, Description = "Full administrative access.", Created = SeedDate, LastModified = SeedDate },
            new Role { Id = 2, Name = RoleNames.User, Description = "Standard authenticated user.", Created = SeedDate, LastModified = SeedDate });
    }
}
