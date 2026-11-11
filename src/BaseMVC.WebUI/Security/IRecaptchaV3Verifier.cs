namespace BaseMVC.WebUI.Security;

public interface IRecaptchaV3Verifier
{
    Task<bool> VerifyAsync(string token, string expectedAction, CancellationToken cancellationToken);
}
