using Microsoft.EntityFrameworkCore;

namespace Platform.Auth;

public interface IUserRepository
{
    Task<UserAccount?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);
}

/// <summary>Reads accounts from USERS through the EF Core context (T046).</summary>
public sealed class UserRepository(AuthDbContext db) : IUserRepository
{
    public Task<UserAccount?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.USERS_USERNAME == username, cancellationToken);
}
