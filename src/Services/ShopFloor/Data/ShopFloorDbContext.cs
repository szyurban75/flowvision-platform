using Microsoft.EntityFrameworkCore;
using Platform.Data;
using ShopFloor.Data.Entities;

namespace ShopFloor.Data;

/// <summary>
/// DbContext for the ShopFloor service (T063). Maps the shared tables used by the scan, start and stop flow.
/// Every mapped table has a ROWVER rowversion, applied by PlatformDbContext, so writes can be checked with If-Match.
/// No raw SQL: queries are written with LINQ against these sets (ADR-001).
/// </summary>
public sealed class ShopFloorDbContext(DbContextOptions<ShopFloorDbContext> options) : PlatformDbContext(options)
{
    public DbSet<TrolleyRequest> TrolleyRequests => Set<TrolleyRequest>();
    public DbSet<TrolleyOperation> TrolleyOperations => Set<TrolleyOperation>();
    public DbSet<TrolleyQrCode> TrolleyQrCodes => Set<TrolleyQrCode>();
    public DbSet<QrCode> QrCodes => Set<QrCode>();
    public DbSet<WorkRecord> WorkRecords => Set<WorkRecord>();
    public DbSet<TimeOfOperationQrCode> TimeOfOperationQrCodes => Set<TimeOfOperationQrCode>();
    public DbSet<RequestOperation> RequestOperations => Set<RequestOperation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TrolleyRequestConfiguration());
        modelBuilder.ApplyConfiguration(new TrolleyOperationConfiguration());
        modelBuilder.ApplyConfiguration(new TrolleyQrCodeConfiguration());
        modelBuilder.ApplyConfiguration(new QrCodeConfiguration());
        modelBuilder.ApplyConfiguration(new WorkRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TimeOfOperationQrCodeConfiguration());
        modelBuilder.ApplyConfiguration(new RequestOperationConfiguration());

        // Base class applies the ROWVER concurrency convention to every entity above, so call it last.
        base.OnModelCreating(modelBuilder);
    }
}
