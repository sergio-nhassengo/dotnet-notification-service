using Application.Common.Interfaces;
using Infrastructure.Notifications.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications.Workers;

public sealed class AttachmentCleanupWorker(IServiceScopeFactory scopes,
    IOptions<NotificationAttachmentOptions> options, IDateTime clock,
    ILogger<AttachmentCleanupWorker> logger) : BackgroundService
{
    public async Task<int> CleanupAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var cutoff = clock.Now.AddDays(-options.Value.RetentionDays);
        var deleted = await db.NotificationAttachments.Where(x => x.CreatedAt < cutoff).ExecuteDeleteAsync(ct);
        logger.LogInformation("Deleted {AttachmentCount} expired attachments older than {Cutoff}", deleted, cutoff);
        return deleted;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await CleanupAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Attachment cleanup failed"); }
            try { await Task.Delay(TimeSpan.FromHours(1), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
