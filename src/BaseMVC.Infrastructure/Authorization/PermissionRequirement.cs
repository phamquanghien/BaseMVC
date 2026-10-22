using Microsoft.AspNetCore.Authorization;

namespace BaseMVC.Infrastructure.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
