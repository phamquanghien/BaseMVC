namespace BaseMVC.Infrastructure.Authorization;

public static class PermissionClaim
{
    public const string Type = "Permission";
    public const string Prefix = "Permissions.";

    public static string Create(string module, string action)
    {
        if (!IsIdentifier(module) || !IsIdentifier(action))
        {
            throw new ArgumentException(
                "Permission module and action must be non-empty ASCII identifiers.");
        }

        return $"{Prefix}{module}.{action}";
    }

    public static bool IsValid(string? permission)
    {
        if (permission is null || !permission.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var segments = permission[Prefix.Length..].Split('.');
        return segments.Length == 2
            && IsIdentifier(segments[0])
            && IsIdentifier(segments[1]);
    }

    private static bool IsIdentifier(string value) =>
        value.Length > 0
        && char.IsAsciiLetter(value[0])
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
}
