namespace Platform.Auth;

public enum SignInStatus
{
    Succeeded,

    /// <summary>Unknown user or wrong password. The same message is used for both, so accounts cannot be enumerated.</summary>
    InvalidCredentials,

    /// <summary>Legacy or empty password value. An administrator must reset it (OQ-005).</summary>
    PasswordResetRequired,

    /// <summary>Password is correct, but the role has no access to this interface.</summary>
    InterfaceNotAllowed,

    UnknownInterface,
}

public sealed record SignInResult(SignInStatus Status, UserAccount? User = null)
{
    public bool Succeeded => Status == SignInStatus.Succeeded;
}

/// <summary>Checks a sign-in attempt against USERS and the interface role rules (FR-009, FR-010).</summary>
public sealed class SignInService(IUserRepository users, IPasswordVerifier passwords)
{
    public async Task<SignInResult> SignInAsync(string username, string password, string interfaceName, CancellationToken cancellationToken = default)
    {
        if (!Interfaces.IsKnown(interfaceName))
        {
            return new SignInResult(SignInStatus.UnknownInterface);
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return new SignInResult(SignInStatus.InvalidCredentials);
        }

        var user = await users.FindByUsernameAsync(username.Trim(), cancellationToken);
        if (user is null)
        {
            return new SignInResult(SignInStatus.InvalidCredentials);
        }

        switch (passwords.Verify(user, password))
        {
            case PasswordCheck.ResetRequired:
                return new SignInResult(SignInStatus.PasswordResetRequired);
            case PasswordCheck.Invalid:
                return new SignInResult(SignInStatus.InvalidCredentials);
        }

        if (!RoleRequirements.IsAllowedToSignIn(interfaceName, user.USERS_ROLE.Trim()))
        {
            return new SignInResult(SignInStatus.InterfaceNotAllowed, user);
        }

        return new SignInResult(SignInStatus.Succeeded, user);
    }
}
