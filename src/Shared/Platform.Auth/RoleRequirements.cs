namespace Platform.Auth;

/// <summary>Interface names used in sign-in and in cookie scheme names (Q1-B: one sign-in per interface).</summary>
public static class Interfaces
{
    public const string Admin = "admin";
    public const string ShopFloor = "shopfloor";
    public const string Documents = "documents";

    public static bool IsKnown(string? name) => name is Admin or ShopFloor or Documents;
}

/// <summary>Role-to-interface rules from open-questions.md (OQ-004, confirmed 2026-11-11).</summary>
public static class RoleRequirements
{
    public const string Administrator = "A";
    public const string Foreman = "B";
    public const string Operator = "U";

    /// <summary>Roles allowed to sign in to each interface.</summary>
    public static IReadOnlySet<string> SignInRoles(string interfaceName) => interfaceName switch
    {
        Interfaces.Admin => new HashSet<string> { Administrator },
        Interfaces.ShopFloor => new HashSet<string> { Operator, Foreman, Administrator },
        Interfaces.Documents => new HashSet<string> { Administrator, Foreman, Operator },
        _ => new HashSet<string>(),
    };

    /// <summary>Documents: editing is for administrators only. Everyone else who can sign in reads.</summary>
    public static bool CanWriteDocuments(string role) => role == Administrator;

    public static bool IsAllowedToSignIn(string interfaceName, string role) =>
        SignInRoles(interfaceName).Contains(role);
}
