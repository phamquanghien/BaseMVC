using MediatR;

namespace BaseMVC.Application.Accounts;

public enum LoginOutcome { Succeeded, Failed, LockedOut }
public sealed record LoginCommand(string Email, string Password, bool RememberMe) : IRequest<LoginOutcome>;
public sealed record LogoutCommand(string? UserId) : IRequest;
