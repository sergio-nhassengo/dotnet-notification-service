using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Entities.Configurations;

public sealed class EmailNotificationConfiguration : IEntityTypeConfiguration<EmailNotification>
{
    public void Configure(EntityTypeBuilder<EmailNotification> b)
    {
        b.ToTable("EmailNotifications"); b.HasKey(x => x.Id);
        b.Property(x => x.CorrelationId).HasMaxLength(200).IsRequired(); b.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
        b.Property(x => x.RecipientEmail).HasMaxLength(320).IsRequired(); b.Property(x => x.RecipientName).HasMaxLength(200);
        b.Property(x => x.SenderEmail).HasMaxLength(320).IsRequired(); b.Property(x => x.SenderName).HasMaxLength(200);
        b.Property(x => x.ReplyTo).HasMaxLength(320); b.Property(x => x.TemplateId).HasMaxLength(100).IsRequired();
        b.Property(x => x.TemplateVariables).HasMaxLength(20000).IsRequired(); b.Property(x => x.Subject).HasMaxLength(200);
        b.Property(x => x.ProviderMessageId).HasMaxLength(500); b.Property(x => x.LastErrorCode).HasMaxLength(100);
        b.Property(x => x.LastErrorMessage).HasMaxLength(500); b.Property(x => x.OriginalTopic).HasMaxLength(249);
        b.HasIndex(x => x.IdempotencyKey).IsUnique(); b.HasIndex(x => x.MessageId).IsUnique(); b.HasIndex(x => x.CorrelationId);
        b.HasIndex(x => new { x.Status, x.NextAttemptAt });
        b.HasMany(x => x.DeliveryAttempts).WithOne(x => x.Notification).HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DeliveryAttemptConfiguration : IEntityTypeConfiguration<DeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<DeliveryAttempt> b)
    {
        b.ToTable("NotificationDeliveryAttempts"); b.HasKey(x => x.Id);
        b.Property(x => x.Provider).HasMaxLength(100).IsRequired(); b.Property(x => x.ProviderMessageId).HasMaxLength(500);
        b.Property(x => x.ErrorCode).HasMaxLength(100); b.Property(x => x.SafeErrorMessage).HasMaxLength(500);
        b.HasIndex(x => new { x.NotificationId, x.AttemptNumber }).IsUnique();
    }
}

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("OutboxMessages"); b.HasKey(x => x.Id); b.Property(x => x.EventType).HasMaxLength(200).IsRequired();
        b.Property(x => x.Topic).HasMaxLength(249).IsRequired(); b.Property(x => x.Payload).IsRequired(); b.Property(x => x.Headers).IsRequired();
        b.Property(x => x.LastError).HasMaxLength(500);
        b.HasIndex(x => new { x.ProcessedAt, x.NextAttemptAt }); b.HasIndex(x => x.MessageKey);
    }
}

public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> b)
    {
        b.ToTable("InboxMessages"); b.HasKey(x => x.Id); b.Property(x => x.ConsumerName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Topic).HasMaxLength(249).IsRequired(); b.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => new { x.ConsumerName, x.MessageId }).IsUnique(); b.HasIndex(x => new { x.Topic, x.Partition, x.Offset });
    }
}

public sealed class NotificationReplayConfiguration : IEntityTypeConfiguration<NotificationReplay>
{
    public void Configure(EntityTypeBuilder<NotificationReplay> b)
    {
        b.ToTable("NotificationReplays"); b.HasKey(x => x.Id); b.Property(x => x.RequestedBy).HasMaxLength(200).IsRequired();
        b.HasIndex(x => new { x.NotificationId, x.RequestedAt });
        b.HasOne(x => x.Notification).WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade);
    }
}
