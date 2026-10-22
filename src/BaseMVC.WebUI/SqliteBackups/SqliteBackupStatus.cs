namespace BaseMVC.WebUI.SqliteBackups;

public sealed class SqliteBackupStatus
{
    private readonly Lock _lock = new();
    private DateTimeOffset? _lastSuccessfulBackupUtc;
    private bool? _lastAttemptSucceeded;

    public (DateTimeOffset? LastSuccessfulBackupUtc, bool? LastAttemptSucceeded) GetSnapshot()
    {
        lock (_lock)
        {
            return (_lastSuccessfulBackupUtc, _lastAttemptSucceeded);
        }
    }

    public void RecordSuccess(DateTimeOffset utcNow)
    {
        lock (_lock)
        {
            _lastSuccessfulBackupUtc = utcNow.ToUniversalTime();
            _lastAttemptSucceeded = true;
        }
    }

    public void RecordFailure()
    {
        lock (_lock)
        {
            _lastAttemptSucceeded = false;
        }
    }
}
