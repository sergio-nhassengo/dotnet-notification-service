using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }

    DbSet<Role> Roles { get; }

    DbSet<EmailNotification> EmailNotifications { get; }
    DbSet<DeliveryAttempt> DeliveryAttempts { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<InboxMessage> InboxMessages { get; }
    DbSet<NotificationReplay> NotificationReplays { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    void ClearTrackedChanges();
}
