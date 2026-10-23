using System.Security.Cryptography;

namespace BaseMVC.WebUI.SqliteBackups;

public sealed class FileSystemBackupArchiveStore(
    ILogger<FileSystemBackupArchiveStore> logger) : IBackupArchiveStore
{
    public async Task<string> ArchiveVerifiedBackupAsync(
        string backupFilePath,
        string archiveDirectory,
        DateTimeOffset utcNow,
        int retentionDays,
        CancellationToken cancellationToken = default)
    {
        if (retentionDays is < 1 or > 3650)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retentionDays),
                "Archive retention must be between 1 and 3650 days.");
        }

        var sourcePath = Path.GetFullPath(backupFilePath);
        var destinationDirectory = Path.GetFullPath(archiveDirectory);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Verified SQLite backup file was not found.", sourcePath);
        }

        Directory.CreateDirectory(destinationDirectory);

        var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(sourcePath));
        var temporaryPath = $"{destinationPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var source = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await source.CopyToAsync(destination, cancellationToken);
                await destination.FlushAsync(cancellationToken);
                destination.Flush(flushToDisk: true);
            }

            SetPrivateFilePermissions(temporaryPath);
            var sourceHash = await HashFileAsync(sourcePath, cancellationToken);
            var destinationHash = await HashFileAsync(temporaryPath, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(sourceHash, destinationHash))
            {
                throw new IOException("Archived SQLite backup failed its SHA-256 copy verification.");
            }

            File.Move(temporaryPath, destinationPath);
            PruneExpiredBackups(destinationDirectory, utcNow, retentionDays);
            return destinationPath;
        }
        catch
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception cleanupException)
            {
                logger.LogError(
                    cleanupException,
                    "Could not remove incomplete archived backup file {TemporaryPath}.",
                    temporaryPath);
            }

            throw;
        }
    }

    private void PruneExpiredBackups(
        string archiveDirectory,
        DateTimeOffset utcNow,
        int retentionDays)
    {
        var cutoffUtc = utcNow.ToUniversalTime().AddDays(-retentionDays);
        foreach (var path in Directory.EnumerateFiles(
            archiveDirectory,
            "basemvc-*.db",
            SearchOption.TopDirectoryOnly))
        {
            if (File.GetLastWriteTimeUtc(path) >= cutoffUtc.UtcDateTime)
            {
                continue;
            }

            File.Delete(path);
            logger.LogInformation("Deleted expired archived SQLite backup {ArchivePath}.", path);
        }
    }

    private static async Task<byte[]> HashFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await SHA256.HashDataAsync(stream, cancellationToken);
    }

    private static void SetPrivateFilePermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
