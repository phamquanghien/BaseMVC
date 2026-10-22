namespace BaseMVC.WebUI.SqliteBackups;

public sealed class SqliteBackupOptions
{
    public bool Enabled { get; set; } = true;

    public string Directory { get; set; } = "App_Data/Backups";

    public int IntervalHours { get; set; } = 24;

    public int RetentionDays { get; set; } = 7;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Directory))
        {
            throw new InvalidOperationException("SqliteBackup:Directory must be configured.");
        }

        if (IntervalHours is < 1 or > 8760)
        {
            throw new InvalidOperationException(
                "SqliteBackup:IntervalHours must be between 1 and 8760.");
        }

        if (RetentionDays is < 1 or > 3650)
        {
            throw new InvalidOperationException(
                "SqliteBackup:RetentionDays must be between 1 and 3650.");
        }
    }
}
