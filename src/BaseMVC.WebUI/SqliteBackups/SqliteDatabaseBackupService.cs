using System.Globalization;
using BaseMVC.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BaseMVC.WebUI.SqliteBackups;

public sealed class SqliteDatabaseBackupService(
    IServiceScopeFactory scopeFactory,
    IOptions<SqliteBackupOptions> options,
    IHostEnvironment environment,
    ILogger<SqliteDatabaseBackupService> logger) : BackgroundService
{
    private const string BackupFilePrefix = "basemvc-";
    private readonly SqliteBackupOptions _options = options.Value;

    public async Task<string> CreateBackupAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        _options.Validate();
        var backupDirectory = Path.GetFullPath(_options.Directory, environment.ContentRootPath);

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (dbContext.Database.GetDbConnection() is not SqliteConnection sourceConnection)
        {
            throw new InvalidOperationException("SQLite backup requires a SQLite database connection.");
        }

        if (string.IsNullOrWhiteSpace(sourceConnection.DataSource)
            || string.Equals(sourceConnection.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase)
            || sourceConnection.DataSource.StartsWith("file::memory:", StringComparison.OrdinalIgnoreCase)
            || new SqliteConnectionStringBuilder(sourceConnection.ConnectionString).Mode
                == SqliteOpenMode.Memory)
        {
            throw new InvalidOperationException(
                "SQLite backup requires a file-backed database, not an in-memory database.");
        }

        Directory.CreateDirectory(backupDirectory);
        SetPrivateDirectoryPermissions(backupDirectory);

        var timestamp = utcNow.ToUniversalTime().ToString(
            "yyyyMMdd'T'HHmmssffff'Z'",
            CultureInfo.InvariantCulture);
        var backupPath = Path.Combine(
            backupDirectory,
            $"{BackupFilePrefix}{timestamp}-{Guid.NewGuid():N}.db");

        try
        {
            if (sourceConnection.State != System.Data.ConnectionState.Open)
            {
                await sourceConnection.OpenAsync(cancellationToken);
            }

            await using var destinationConnection = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = backupPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                    Pooling = false
                }.ToString());
            await destinationConnection.OpenAsync(cancellationToken);
            SetPrivateFilePermissions(backupPath);

            sourceConnection.BackupDatabase(destinationConnection);
            await VerifyIntegrityAsync(destinationConnection, cancellationToken);
            await PruneExpiredBackupsAsync(backupDirectory, utcNow, cancellationToken);

            logger.LogInformation("Created and verified SQLite backup at {BackupPath}.", backupPath);
            return backupPath;
        }
        catch
        {
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
            catch (Exception cleanupException)
            {
                logger.LogError(
                    cleanupException,
                    "Could not remove incomplete SQLite backup at {BackupPath}.",
                    backupPath);
            }

            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _options.Validate();
        if (!_options.Enabled)
        {
            logger.LogInformation("Automated SQLite backups are disabled.");
            return;
        }

        var interval = TimeSpan.FromHours(_options.IntervalHours);
        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await CreateBackupAsync(DateTimeOffset.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "SQLite backup failed. It will be retried in {RetryInterval}.",
                    interval);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private Task PruneExpiredBackupsAsync(
        string backupDirectory,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        var cutoffUtc = utcNow.ToUniversalTime().AddDays(-_options.RetentionDays);
        foreach (var backupPath in Directory.EnumerateFiles(
            backupDirectory,
            $"{BackupFilePrefix}*.db",
            SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.GetLastWriteTimeUtc(backupPath) >= cutoffUtc.UtcDateTime)
            {
                continue;
            }

            File.Delete(backupPath);
            logger.LogInformation("Deleted expired SQLite backup {BackupPath}.", backupPath);
        }

        return Task.CompletedTask;
    }

    private static async Task VerifyIntegrityAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var integrityResult = await command.ExecuteScalarAsync(cancellationToken);
        if (!string.Equals(integrityResult as string, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"SQLite backup integrity check failed: {integrityResult ?? "no result"}.");
        }
    }

    private static void SetPrivateDirectoryPermissions(string directory)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                directory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static void SetPrivateFilePermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
