using MediatR;

namespace BaseMVC.Application.Accounts;

public sealed class LoginHandler(IAuthenticationService service) : IRequestHandler<LoginCommand, LoginOutcome>
{
    public Task<LoginOutcome> Handle(LoginCommand request, CancellationToken cancellationToken) =>
        service.LoginAsync(request.Email, request.Password, request.RememberMe, cancellationToken);
}

public sealed class LogoutHandler(IAuthenticationService service) : IRequestHandler<LogoutCommand>
{
    public Task Handle(LogoutCommand request, CancellationToken cancellationToken) =>
        service.LogoutAsync(request.UserId, cancellationToken);
}
