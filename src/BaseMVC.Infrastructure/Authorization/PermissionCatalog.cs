using BaseMVC.Infrastructure.Identity;

namespace BaseMVC.Infrastructure.Authorization;

public sealed record PermissionDefinition(string Module, string Action)
{
    public string Name => PermissionClaim.Create(Module, Action);
}

public static class PermissionCatalog
{
    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        new("Users", "Read"),
        new("Users", "Create"),
        new("Users", "Update"),
        new("Users", "Delete"),
        new("Roles", "Read"),
        new("Roles", "Create"),
        new("Roles", "Update"),
        new("Roles", "Delete"),
        new("Access", "Read"),
        new("Access", "Manage")
    ];

    public static IReadOnlyList<PermissionDefinition> ForRole(string roleName) =>
        roleName switch
        {
            IdentitySeedRoles.SuperAdmin => All,
            IdentitySeedRoles.Administrator => All
                .Where(permission =>
                    permission.Module is "Users" or "Roles"
                    || permission is { Module: "Access", Action: "Read" })
                .ToArray(),
            _ => []
        };
}
