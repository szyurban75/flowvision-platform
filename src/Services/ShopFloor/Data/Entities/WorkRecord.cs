using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShopFloor.Data.Entities;

/// <summary>
/// Row of TIME_OF_OPERATIONS: the work record (start, stop, quantities, cost) for one operation by one operator on one machine.
/// Column names and nullability verified against the live FLOWVISION2_TEST schema and the desktop CTImeOfOperation SQL
/// (TOW_* columns). The desktop writes TOW_COST_GOOD and TOW_COST_BAD as the stored cost figures.
/// ROWVER is the rowversion added by migration 2.0.36.
/// </summary>
[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Property names match the shared TIME_OF_OPERATIONS columns.")]
public sealed class WorkRecord
{
    public int TOW_ID { get; set; }
    public int? TOW_REQ_OPERATION_ID { get; set; }
    public int? TOW_DIC_ID { get; set; }
    public DateTime? TOW_START_WORK { get; set; }
    public DateTime? TOW_STOP_WORK { get; set; }
    public string? TOW_STATUS { get; set; }
    public int? TOW_USERS_ID { get; set; }
    public int? TOW_MACHINE_ID { get; set; }
    public double? TOW_QUANTITY_GOOD { get; set; }
    public double? TOW_QUANTITY_BAD { get; set; }
    public double? TOW_COST_GOOD { get; set; }
    public double? TOW_COST_BAD { get; set; }
    public int? TOW_TR_REQ_OP_ID { get; set; }
    public int? TOW_QR_REQ_OP_ID { get; set; }
    public byte[] ROWVER { get; set; } = [];
}

/// <summary>
/// Row of TIME_OF_OPERATIONS_QRCODES: links a QR code (QR_ID) to the work record (TOO_ID) it started or stopped.
/// The table is empty in FLOWVISION2_TEST.
/// </summary>
[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Property names match the shared TIME_OF_OPERATIONS_QRCODES columns.")]
public sealed class TimeOfOperationQrCode
{
    public int TOOQR_ID { get; set; }
    public int TOOQR_QR_ID { get; set; }
    public int TOOQR_TOO_ID { get; set; }
    public byte[] ROWVER { get; set; } = [];
}

/// <summary>EF Core mapping for <see cref="WorkRecord"/>. Applied by ShopFloorDbContext (T063).</summary>
public sealed class WorkRecordConfiguration : IEntityTypeConfiguration<WorkRecord>
{
    public void Configure(EntityTypeBuilder<WorkRecord> builder)
    {
        builder.ToTable("TIME_OF_OPERATIONS");
        builder.HasKey(w => w.TOW_ID);
        builder.Property(w => w.TOW_STATUS).HasMaxLength(1).IsFixedLength();
        builder.Property(w => w.ROWVER).IsRowVersion();
    }
}

/// <summary>EF Core mapping for <see cref="TimeOfOperationQrCode"/>. Applied by ShopFloorDbContext (T063).</summary>
public sealed class TimeOfOperationQrCodeConfiguration : IEntityTypeConfiguration<TimeOfOperationQrCode>
{
    public void Configure(EntityTypeBuilder<TimeOfOperationQrCode> builder)
    {
        builder.ToTable("TIME_OF_OPERATIONS_QRCODES");
        builder.HasKey(t => t.TOOQR_ID);
        builder.Property(t => t.ROWVER).IsRowVersion();
    }
}
