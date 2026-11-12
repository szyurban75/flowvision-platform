using Microsoft.AspNetCore.Identity;

namespace Platform.Auth;

public enum PasswordCheck
{
    Valid,
    Invalid,

    /// <summary>The stored value is a legacy GetHashCode value, empty, or NULL. An administrator must reset it (OQ-005).</summary>
    ResetRequired,
}

public interface IPasswordVerifier
{
    PasswordCheck Verify(UserAccount user, string password);

    string HashPassword(UserAccount user, string password);
}

/// <summary>
/// PBKDF2 hashes from ASP.NET Core PasswordHasher (OQ-005). Only the Identity V3 format is accepted on the web.
/// Legacy values (sPassword.GetHashCode().ToString()) are refused and must be reset.
/// </summary>
public sealed class PasswordVerifier : IPasswordVerifier
{
    private readonly PasswordHasher<UserAccount> _hasher = new();

    public PasswordCheck Verify(UserAccount user, string password)
    {
        var stored = user.USERS_PASSWORD;
        if (!IsIdentityV3Hash(stored))
        {
            return PasswordCheck.ResetRequired;
        }

        var result = _hasher.VerifyHashedPassword(user, stored!, password);
        return result is PasswordVerificationResult.Failed ? PasswordCheck.Invalid : PasswordCheck.Valid;
    }

    public string HashPassword(UserAccount user, string password) => _hasher.HashPassword(user, password);

    /// <summary>Identity V3 hashes are base64 text whose first byte is 0x01 and that decode to a fixed minimum length.</summary>
    internal static bool IsIdentityV3Hash(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(stored);
            return bytes.Length >= 13 && bytes[0] == 0x01;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
