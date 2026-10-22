namespace BaseMVC.WebUI.Security;

public sealed class AccountRateLimitOptions
{
    public int LoginPermitLimit { get; set; } = 5;

    public int LoginWindowSeconds { get; set; } = 300;

    public int PasswordResetPermitLimit { get; set; } = 3;

    public int PasswordResetWindowSeconds { get; set; } = 900;

    public void Validate()
    {
        ValidateLimit(LoginPermitLimit, nameof(LoginPermitLimit));
        ValidateWindow(LoginWindowSeconds, nameof(LoginWindowSeconds));
        ValidateLimit(PasswordResetPermitLimit, nameof(PasswordResetPermitLimit));
        ValidateWindow(PasswordResetWindowSeconds, nameof(PasswordResetWindowSeconds));
    }

    private static void ValidateLimit(int value, string name)
    {
        if (value is < 1 or > 1000)
        {
            throw new InvalidOperationException(
                $"RateLimits:Account:{name} must be between 1 and 1000.");
        }
    }

    private static void ValidateWindow(int value, string name)
    {
        if (value is < 1 or > 86400)
        {
            throw new InvalidOperationException(
                $"RateLimits:Account:{name} must be between 1 and 86400 seconds.");
        }
    }
}
