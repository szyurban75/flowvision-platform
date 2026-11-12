using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Platform.Data;

/// <summary>
/// Base DbContext for all platform services (ADR-001). Shared tables carry a ROWVER rowversion column
/// (migration 2.0.36); every mapped entity with that property gets it configured as a concurrency token.
/// </summary>
public abstract class PlatformDbContext : DbContext
{
    /// <summary>Name of the rowversion column added to every shared table.</summary>
    public const string RowVersionColumn = "ROWVER";

    protected PlatformDbContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ApplyRowVersionConvention(modelBuilder);
    }

    /// <summary>
    /// Web writes check the row version through If-Match (Q2, Q3). Setting the property as a concurrency token
    /// makes EF Core raise DbUpdateConcurrencyException when the stored value differs.
    /// Rows are never ordered or filtered by this column, so the checked queries stay explicit.
    /// </summary>
    public static void ApplyRowVersionConvention(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entity in modelBuilder.Model.GetEntityTypes())
        {
            var property = entity.FindProperty(RowVersionColumn);
            if (property is null || property.ClrType != typeof(byte[]))
            {
                continue;
            }

            property.SetColumnType("rowversion");
            property.ValueGenerated = ValueGenerated.OnAddOrUpdate;
            property.IsConcurrencyToken = true;
        }
    }
}
