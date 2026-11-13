using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShopFloor.Data.Entities;

/// <summary>
/// Row of REQUEST_OPERATIONS: one operation of an order (request), with its target and real time and cost.
/// Mapped to the live FLOWVISION2_TEST schema (11,374 rows). The name was verified against the live database.
/// Only REQ_OP_TARGET_TIME_WORK and REQ_OP_TARGET_TIME_SETUP are NOT NULL live; the other columns are nullable.
/// ROWVER is the rowversion added by migration 2.0.36.
/// </summary>
[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Property names match the shared REQUEST_OPERATIONS columns.")]
public sealed class RequestOperation
{
    public int REQ_OP_ID { get; set; }
    public int? REQ_OP_REQ_ID { get; set; }
    public int? REQ_OP_ITEMS_ID { get; set; }
    public int? REQ_OP_GRP_ID { get; set; }
    public string? REQ_OP_GRP_NAME { get; set; }
    public DateTime? REQ_OP_WORK_START { get; set; }
    public DateTime? REQ_OP_WORK_STOP { get; set; }
    public string? REQ_OP_STATUS { get; set; }
    public int? REQ_OP_SEQUENCE_NUMBER { get; set; }
    public double REQ_OP_TARGET_TIME_WORK { get; set; }
    public double REQ_OP_TARGET_TIME_SETUP { get; set; }
    public double? REQ_OP_TARGET_COST_WORK { get; set; }
    public double? REQ_OP_TARGET_COST_SETUP { get; set; }
    public double? REQ_OP_REAL_TIME_WORK { get; set; }
    public double? REQ_OP_REAL_TIME_SETUP { get; set; }
    public double? REQ_OP_REAL_COST_WORK { get; set; }
    public double? REQ_OP_REAL_COST_SETUP { get; set; }
    public int? REQ_MACHINE_ID { get; set; }
    public string? REQ_OP_OPERATION_CODE { get; set; }
    public byte[] ROWVER { get; set; } = [];
}

/// <summary>EF Core mapping for <see cref="RequestOperation"/>. Applied by ShopFloorDbContext (T063).</summary>
public sealed class RequestOperationConfiguration : IEntityTypeConfiguration<RequestOperation>
{
    public void Configure(EntityTypeBuilder<RequestOperation> builder)
    {
        builder.ToTable("REQUEST_OPERATIONS");
        builder.HasKey(r => r.REQ_OP_ID);
        builder.Property(r => r.REQ_OP_GRP_NAME).HasMaxLength(32);
        builder.Property(r => r.REQ_OP_STATUS).HasMaxLength(1).IsFixedLength();
        builder.Property(r => r.REQ_OP_OPERATION_CODE).HasMaxLength(36);
        builder.Property(r => r.ROWVER).IsRowVersion();
    }
}
