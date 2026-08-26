using Domain.Constants;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Entities.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
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
            new Role { Id = 1, Name = RoleNames.Admin, Description = "Full administrative access." },
            new Role { Id = 2, Name = RoleNames.User, Description = "Standard authenticated user." });
    }
}
