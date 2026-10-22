using BaseMVC.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BaseMVC.WebUI.HealthChecks;

public sealed class SqliteReadinessHealthCheck(ApplicationDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
    }
}
