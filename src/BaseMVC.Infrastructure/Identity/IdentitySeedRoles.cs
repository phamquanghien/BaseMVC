namespace BaseMVC.Infrastructure.Identity;

public static class IdentitySeedRoles
{
    public const string Administrator = "Administrator";
    public const string SuperAdmin = "SuperAdmin";

    public static IReadOnlyList<string> All { get; } =
    [
        Administrator,
        SuperAdmin
    ];
}
