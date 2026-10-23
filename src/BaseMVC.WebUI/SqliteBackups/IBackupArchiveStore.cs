namespace BaseMVC.WebUI.SqliteBackups;

public interface IBackupArchiveStore
{
    Task<string> ArchiveVerifiedBackupAsync(
        string backupFilePath,
        string archiveDirectory,
        DateTimeOffset utcNow,
        int retentionDays,
        CancellationToken cancellationToken = default);
}
