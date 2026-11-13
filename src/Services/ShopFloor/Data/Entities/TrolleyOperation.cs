using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShopFloor.Data.Entities;

/// <summary>
/// Row of TROLLEY_OPERATIONS: one operation of a trolley request, in sequence.
/// Mapped to the live FLOWVISION2_TEST schema. Where the live table differs from db_FlowVision_2.0.35.sql, the live definition is used:
/// TROP_SEQUENCE_NUMBER is nullable live (NOT NULL in the script), and TROP_STATUS has no default live (default 'N' in the script).
/// ROWVER is the rowversion added by migration 2.0.36.
/// </summary>
[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Property names match the shared TROLLEY_OPERATIONS columns.")]
public sealed class TrolleyOperation
{
    public int TROP_ID { get; set; }
    public int TROP_REQOP_ID { get; set; }
    public int TROP_TR_ID { get; set; }
    public int TROP_TR_REQ_ID { get; set; }
    public DateTime? TROP_DATE_START { get; set; }
    public DateTime? TROP_DATE_STOP { get; set; }
    public string? TROP_STATUS { get; set; }
    public int? TROP_SEQUENCE_NUMBER { get; set; }
    public byte[] ROWVER { get; set; } = [];
}

/// <summary>EF Core mapping for <see cref="TrolleyOperation"/>. Applied by ShopFloorDbContext (T063).</summary>
public sealed class TrolleyOperationConfiguration : IEntityTypeConfiguration<TrolleyOperation>
{
    public void Configure(EntityTypeBuilder<TrolleyOperation> builder)
    {
        builder.ToTable("TROLLEY_OPERATIONS");
        builder.HasKey(t => t.TROP_ID);
        builder.Property(t => t.TROP_STATUS).HasMaxLength(1);
        builder.Property(t => t.ROWVER).IsRowVersion();
        builder.HasIndex(t => new { t.TROP_TR_ID, t.TROP_REQOP_ID }).HasDatabaseName("TROLLEY_OPERATIONS_IDX");
    }
}
