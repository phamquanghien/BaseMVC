using BaseMVC.WebUI.SqliteBackups;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BaseMVC.WebUI.HealthChecks;

public sealed class SqliteBackupHealthCheck(
    IOptions<SqliteBackupOptions> options,
    SqliteBackupStatus backupStatus,
    IHostEnvironment environment,
    ILogger<SqliteBackupHealthCheck> logger) : IHealthCheck
{
    private const string BackupFilePattern = "basemvc-*.db";

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (backupStatus.GetSnapshot().LastAttemptSucceeded == false)
        {
            logger.LogError("The most recent SQLite backup attempt failed.");
            return Unhealthy("The most recent SQLite backup attempt failed.");
        }

        var backupOptions = options.Value;
        backupOptions.Validate();
        var backupDirectory = Path.GetFullPath(
            backupOptions.Directory,
            environment.ContentRootPath);

        if (!Directory.Exists(backupDirectory))
        {
            logger.LogError(
                "SQLite backup directory does not exist: {BackupDirectory}.",
                backupDirectory);
            return Unhealthy("SQLite backup directory does not exist.");
        }

        FileInfo? latestBackup;
        try
        {
            VerifyDirectoryWritable(backupDirectory);
            latestBackup = Directory
                .EnumerateFiles(backupDirectory, BackupFilePattern, SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch (IOException exception)
        {
            logger.LogError(
                exception,
                "SQLite backup storage could not be read or written: {BackupDirectory}.",
                backupDirectory);
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "SQLite backup storage could not be read or written."));
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogError(
                exception,
                "SQLite backup storage access was denied: {BackupDirectory}.",
                backupDirectory);
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "SQLite backup storage access was denied."));
        }

        if (latestBackup is null)
        {
            return Unhealthy("No SQLite backup has been created yet.");
        }

        var maximumAge = TimeSpan.FromHours(backupOptions.IntervalHours * 2d);
        var backupAge = DateTimeOffset.UtcNow - latestBackup.LastWriteTimeUtc;
        if (backupAge > maximumAge)
        {
            logger.LogError(
                "The latest SQLite backup is stale. Its age is {BackupAge}; maximum allowed age is {MaximumAge}.",
                backupAge,
                maximumAge);
            return Unhealthy("The latest SQLite backup is older than two backup intervals.");
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            "The SQLite backup directory is writable and a recent backup is available."));
    }

    private static void VerifyDirectoryWritable(string backupDirectory)
    {
        var probePath = Path.Combine(
            backupDirectory,
            $".basemvc-backup-health-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(
                probePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                FileOptions.WriteThrough))
            {
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(
                        probePath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }

                stream.WriteByte(0);
                stream.Flush(flushToDisk: true);
            }
        }
        finally
        {
            if (File.Exists(probePath))
            {
                File.Delete(probePath);
            }
        }
    }

    private static Task<HealthCheckResult> Unhealthy(string description) =>
        Task.FromResult(HealthCheckResult.Unhealthy(description));
}
