using Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Entities.Configurations;

public sealed class EmailNotificationConfiguration : IEntityTypeConfiguration<EmailNotification>
{
    public void Configure(EntityTypeBuilder<EmailNotification> b)
    {
        b.ToTable("EmailNotifications"); b.HasKey(x => x.Id);
        b.Property(x => x.CorrelationId).HasMaxLength(200).IsRequired(); b.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
        b.Property(x => x.SenderEmail).HasMaxLength(320).IsRequired(); b.Property(x => x.SenderName).HasMaxLength(200);
        b.Property(x => x.TemplateId).HasMaxLength(100).IsRequired();
        b.Property(x => x.TemplateVariables).HasMaxLength(20000).IsRequired(); b.Property(x => x.Subject).HasMaxLength(200);
        b.Property(x => x.LastErrorCode).HasMaxLength(100);
        b.Property(x => x.LastErrorMessage).HasMaxLength(500); 
        b.Property(x => x.ApplicationId).HasMaxLength(100).HasDefaultValue("legacy").IsRequired();
        b.Property(x => x.RequestHash).HasMaxLength(64);
        b.HasIndex(x => new { x.ApplicationId, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => x.MessageId).IsUnique(); b.HasIndex(x => x.CorrelationId);
        b.HasIndex(x => new { x.Status, x.NextAttemptAt });
    }
}

