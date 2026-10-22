using BaseMVC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BaseMVC.WebUI.Auditing;

public sealed class AuditLogRetentionService(
    IServiceScopeFactory scopeFactory,
    ILogger<AuditLogRetentionService> logger) : BackgroundService
{
    public const int RetentionDays = 90;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);

    public async Task<int> DeleteExpiredEntriesAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        var cutoffUtc = utcNow.ToUniversalTime().AddDays(-RetentionDays);
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var deletedCount = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""DELETE FROM "AuditLogEntries" WHERE "OccurredAtUtc" < {cutoffUtc}""",
            cancellationToken);

        if (deletedCount > 0)
        {
            logger.LogInformation(
                "Deleted {AuditLogCount} audit log entries older than {RetentionDays} days.",
                deletedCount,
                RetentionDays);
        }

        return deletedCount;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval);
        do
        {
            try
            {
                await DeleteExpiredEntriesAsync(DateTimeOffset.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Audit log retention cleanup failed. It will be retried in {RetryInterval}.",
                    CleanupInterval);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
