using Platform.Auth;
using Xunit;

namespace Platform.Services.Tests;

public class SignInTests
{
    private sealed class FakeUsers(params UserAccount[] accounts) : IUserRepository
    {
        public Task<UserAccount?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
            Task.FromResult(accounts.FirstOrDefault(a => a.USERS_USERNAME == username));
    }

    private static readonly PasswordVerifier Verifier = new();

    private static UserAccount Account(string name, string role, string? passwordHash) => new()
    {
        USERS_ID = 1000,
        USERS_USERNAME = name,
        USERS_NAME = name,
        USERS_ROLE = role,
        USERS_PASSWORD = passwordHash,
    };

    private static UserAccount WithHashedPassword(string name, string role, string password)
    {
        var user = Account(name, role, null);
        user.USERS_PASSWORD = Verifier.HashPassword(user, password);
        return user;
    }

    private static SignInService Service(params UserAccount[] accounts) => new(new FakeUsers(accounts), Verifier);

    [Fact]
    public async Task ValidSignIn_ToAdminInterface_Succeeds()
    {
        var service = Service(WithHashedPassword("anna", "A", "correct-horse"));

        var result = await service.SignInAsync("anna", "correct-horse", Interfaces.Admin);

        Assert.Equal(SignInStatus.Succeeded, result.Status);
        Assert.Equal("anna", result.User!.USERS_USERNAME);
    }

    [Fact]
    public async Task WrongPassword_IsRejected_WithSameMessageAsUnknownUser()
    {
        var service = Service(WithHashedPassword("anna", "A", "correct-horse"));

        var wrong = await service.SignInAsync("anna", "wrong", Interfaces.Admin);
        var unknown = await service.SignInAsync("nobody", "correct-horse", Interfaces.Admin);

        Assert.Equal(SignInStatus.InvalidCredentials, wrong.Status);
        Assert.Equal(SignInStatus.InvalidCredentials, unknown.Status);
    }

    [Fact]
    public async Task OperatorWithoutAdminRole_CannotSignInToAdminInterface()
    {
        var service = Service(WithHashedPassword("jan", "U", "pw-123"));

        var result = await service.SignInAsync("jan", "pw-123", Interfaces.Admin);

        Assert.Equal(SignInStatus.InterfaceNotAllowed, result.Status);
    }

    [Fact]
    public async Task OperatorCanSignInToShopFloor()
    {
        var service = Service(WithHashedPassword("jan", "U", "pw-123"));

        var result = await service.SignInAsync("jan", "pw-123", Interfaces.ShopFloor);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("1234567890")]   // legacy GetHashCode value
    [InlineData("")]
    [InlineData(null)]
    public async Task LegacyOrEmptyPassword_RequiresReset(string? stored)
    {
        var service = Service(Account("old", "A", stored));

        var result = await service.SignInAsync("old", "anything", Interfaces.Admin);

        Assert.Equal(SignInStatus.PasswordResetRequired, result.Status);
    }

    [Fact]
    public async Task UnknownInterface_IsRejected()
    {
        var service = Service(WithHashedPassword("anna", "A", "correct-horse"));

        var result = await service.SignInAsync("anna", "correct-horse", "reports");

        Assert.Equal(SignInStatus.UnknownInterface, result.Status);
    }

    [Fact]
    public void Documents_WriteIsForAdministratorsOnly()
    {
        Assert.True(RoleRequirements.CanWriteDocuments("A"));
        Assert.False(RoleRequirements.CanWriteDocuments("B"));
        Assert.False(RoleRequirements.CanWriteDocuments("U"));
    }
}
