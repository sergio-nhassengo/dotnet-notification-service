using Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Entities.Configurations;

public sealed class NotificationAttachmentConfiguration : IEntityTypeConfiguration<NotificationAttachment>
{
    public void Configure(EntityTypeBuilder<NotificationAttachment> b)
    {
        b.ToTable("NotificationAttachments"); b.HasKey(x => x.Id);
        b.Property(x => x.ApplicationId).HasMaxLength(100).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(200).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Content).IsRequired();
        b.HasIndex(x => new { x.ApplicationId, x.CreatedAt });
    }
}
