using Platform.Auth;
using Xunit;

namespace Platform.Services.Tests;

/// <summary>T047: a password set by the administrator reset is accepted by the sign-in verifier, and legacy values are not.</summary>
public class PasswordResetTests
{
    [Fact]
    public void ResetHash_IsAcceptedBySignInVerifier()
    {
        var verifier = new PasswordVerifier();
        var user = new UserAccount { USERS_ID = 7, USERS_USERNAME = "operator1", USERS_ROLE = "U" };
        user.USERS_PASSWORD = verifier.HashPassword(user, "NewSecret123");

        Assert.Equal(PasswordCheck.Valid, verifier.Verify(user, "NewSecret123"));
        Assert.Equal(PasswordCheck.Invalid, verifier.Verify(user, "wrong-password"));
    }

    [Fact]
    public void LegacyValue_IsNotAcceptedAfterAllowingReset()
    {
        var verifier = new PasswordVerifier();
        var user = new UserAccount { USERS_ID = 8, USERS_USERNAME = "legacy", USERS_ROLE = "U", USERS_PASSWORD = "1234567890" };

        Assert.Equal(PasswordCheck.ResetRequired, verifier.Verify(user, "1234567890"));
    }
}
