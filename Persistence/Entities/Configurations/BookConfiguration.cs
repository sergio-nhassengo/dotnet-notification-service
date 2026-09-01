using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Entities.Configurations;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.Property(r => r.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.Description)
            .HasMaxLength(500);

        builder.Property(r => r.AuthorName)
            .HasMaxLength(500);
        
        builder.Property(r => r.AuthorEmail)
            .HasMaxLength(500);
        
        builder.HasIndex(r => r.Name)
            .IsUnique();
        
        
    }
}
