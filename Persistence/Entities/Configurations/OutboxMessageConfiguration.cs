using Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Entities.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("OutboxMessages"); b.HasKey(x => x.Id); 
        b.Property(x => x.Topic).HasMaxLength(249).IsRequired(); b.Property(x => x.Payload).IsRequired(); b.Property(x => x.Headers).IsRequired();
        b.Property(x => x.LastError).HasMaxLength(500);
        b.HasIndex(x => new { x.ProcessedAt, x.NextAttemptAt }); b.HasIndex(x => x.MessageKey);
    }
}
