namespace BaseMVC.Application.Accounts;

public interface IAuthenticationService
{
    Task<LoginOutcome> LoginAsync(string email, string password, bool rememberMe, CancellationToken cancellationToken);
    Task LogoutAsync(string? userId, CancellationToken cancellationToken);
}
