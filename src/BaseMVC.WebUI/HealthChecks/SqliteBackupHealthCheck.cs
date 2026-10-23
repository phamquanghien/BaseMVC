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
        var maximumAge = TimeSpan.FromHours(backupOptions.IntervalHours * 2d);
        var localResult = CheckBackupDirectory(
            backupDirectory,
            "SQLite backup",
            maximumAge);
        if (localResult is not null)
        {
            return localResult;
        }

        if (!string.IsNullOrWhiteSpace(backupOptions.ArchiveDirectory))
        {
            var archiveDirectory = Path.GetFullPath(
                backupOptions.ArchiveDirectory,
                environment.ContentRootPath);
            var archiveResult = CheckBackupDirectory(
                archiveDirectory,
                "SQLite backup archive",
                maximumAge);
            if (archiveResult is not null)
            {
                return archiveResult;
            }
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            "Local SQLite backup and configured archive are writable and recent."));
    }

    private Task<HealthCheckResult>? CheckBackupDirectory(
        string backupDirectory,
        string storageName,
        TimeSpan maximumAge)
    {
        if (!Directory.Exists(backupDirectory))
        {
            logger.LogError(
                "{StorageName} directory does not exist: {BackupDirectory}.",
                storageName,
                backupDirectory);
            return Unhealthy($"{storageName} directory does not exist.");
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
                "{StorageName} storage could not be read or written: {BackupDirectory}.",
                storageName,
                backupDirectory);
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"{storageName} storage could not be read or written."));
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogError(
                exception,
                "{StorageName} storage access was denied: {BackupDirectory}.",
                storageName,
                backupDirectory);
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"{storageName} storage access was denied."));
        }

        if (latestBackup is null)
        {
            return Unhealthy($"No {storageName} file has been created yet.");
        }

        var backupAge = DateTimeOffset.UtcNow - latestBackup.LastWriteTimeUtc;
        if (backupAge > maximumAge)
        {
            logger.LogError(
                "The latest {StorageName} file is stale. Its age is {BackupAge}; maximum allowed age is {MaximumAge}.",
                storageName,
                backupAge,
                maximumAge);
            return Unhealthy($"The latest {storageName} file is older than two backup intervals.");
        }

        return null;
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
