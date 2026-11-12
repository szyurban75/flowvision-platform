using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Data;

namespace Platform.Auth;

/// <summary>Row of the shared USERS table. Roles: A administrator, B foreman, U operator (USERS_ROLE, OQ-004).</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Property names match the shared USERS table columns.")]
public sealed class UserAccount
{
    public int USERS_ID { get; set; }
    public string USERS_USERNAME { get; set; } = "";
    public string USERS_NAME { get; set; } = "";
    public string USERS_ROLE { get; set; } = "";
    public string? USERS_PASSWORD { get; set; }
    public byte[] ROWVER { get; set; } = [];
}

/// <summary>DbContext for sign-in. Reads the shared USERS table through EF Core (ADR-001: no raw SQL in repositories).</summary>
public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : PlatformDbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("USERS");
            entity.HasKey(u => u.USERS_ID);
            entity.Property(u => u.USERS_USERNAME).HasMaxLength(30).IsRequired();
            entity.Property(u => u.USERS_NAME).HasMaxLength(50);
            entity.Property(u => u.USERS_ROLE).HasMaxLength(1).IsFixedLength();
            entity.Property(u => u.USERS_PASSWORD).HasMaxLength(128);
        });
        base.OnModelCreating(modelBuilder);
    }
}
