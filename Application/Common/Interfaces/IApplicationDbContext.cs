using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Interfaces;

public interface IApplicationDbContext
{
    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }

    DbSet<NotificationAttachment> NotificationAttachments { get; }

    DbSet<EmailNotification> EmailNotifications { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    void ClearTrackedChanges();
}
