using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Entities.Configurations;

public class TodoListConfiguration : IEntityTypeConfiguration<TodoList>
{
    public void Configure(EntityTypeBuilder<TodoList> builder)
    {
        builder.Property(l => l.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasMany(l => l.Items)
            .WithOne(i => i.TodoList)
            .HasForeignKey(i => i.TodoListId);
    }
}
