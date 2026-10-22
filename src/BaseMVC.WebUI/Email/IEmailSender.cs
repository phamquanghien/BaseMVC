namespace BaseMVC.WebUI.Email;

public interface IEmailSender
{
    Task SendPasswordResetAsync(
        string recipient,
        string resetUrl,
        CancellationToken cancellationToken = default);
}
