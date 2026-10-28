using BaseMVC.Application.Accounts;

namespace BaseMVC.WebUI.Email;

public sealed class EmailDeliveryException(string message, Exception innerException)
    : PasswordResetEmailDeliveryException(message, innerException);
