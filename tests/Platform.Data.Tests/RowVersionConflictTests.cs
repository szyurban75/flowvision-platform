using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Platform.Data.Tests;

/// <summary>T032: a stale write on a row with ROWVER raises DbUpdateConcurrencyException.</summary>
public class RowVersionConflictTests
{
    private sealed class ProbeContext(DbContextOptions<ProbeContext> options) : PlatformDbContext(options)
    {
        public DbSet<Probe> Probes => Set<Probe>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Probe>(entity =>
            {
                entity.ToTable("ROW_VERSION_PROBE");
                entity.HasKey(p => p.ID);
                entity.Property(p => p.ID).ValueGeneratedNever();
                entity.Property(p => p.Label).HasMaxLength(50);
            });
            base.OnModelCreating(modelBuilder);
        }
    }

    private sealed class Probe
    {
        public int ID { get; set; }
        public string Label { get; set; } = "";
        public byte[] ROWVER { get; set; } = [];
    }

    [SkippableFact]
    public async Task StaleWrite_RaisesConcurrencyException()
    {
        var connectionString = TestDatabase.EmptyConnection;
        Skip.If(connectionString is null, "PLATFORM_TEST_EMPTY_CONNECTION is not set.");

        await using (var setup = new SqlConnection(connectionString))
        {
            await setup.OpenAsync();
            await using var command = new SqlCommand(
                "IF OBJECT_ID(N'dbo.ROW_VERSION_PROBE', N'U') IS NOT NULL DROP TABLE dbo.ROW_VERSION_PROBE; " +
                "CREATE TABLE dbo.ROW_VERSION_PROBE (ID int NOT NULL PRIMARY KEY, Label varchar(50) NOT NULL, ROWVER rowversion NOT NULL); " +
                "INSERT INTO dbo.ROW_VERSION_PROBE (ID, Label) VALUES (1, 'first');", setup);
            await command.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<ProbeContext>().UseSqlServer(connectionString).Options;
        await using var first = new ProbeContext(options);
        await using var second = new ProbeContext(options);

        var a = await first.Probes.AsNoTracking().SingleAsync(p => p.ID == 1);
        var b = await second.Probes.AsNoTracking().SingleAsync(p => p.ID == 1);

        // The second writer saves first, so the first writer's copy of the row version is now stale.
        second.Probes.Attach(b).Entity.Label = "second";
        await second.SaveChangesAsync();

        first.Probes.Attach(a).Entity.Label = "first-stale";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => first.SaveChangesAsync());
    }
}
