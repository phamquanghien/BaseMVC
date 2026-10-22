namespace BaseMVC.WebUI.Security;

public static class AccountRateLimitPolicies
{
    public const string Login = "account-login";
    public const string PasswordReset = "password-reset";
    public const string LoginPath = "/Account/Login";
    public const string PasswordResetPath = "/Account/ForgotPassword";
}
