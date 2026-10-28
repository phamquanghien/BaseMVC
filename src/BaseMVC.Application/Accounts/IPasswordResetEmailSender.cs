namespace BaseMVC.Application.Accounts;

public class PasswordResetEmailDeliveryException(string message, Exception innerException)
    : Exception(message, innerException);

public interface IPasswordResetEmailSender
{
    Task SendPasswordResetAsync(string recipient, string resetUrl, CancellationToken cancellationToken = default);
}
