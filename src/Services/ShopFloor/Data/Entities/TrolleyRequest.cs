using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShopFloor.Data.Entities;

/// <summary>
/// Row of TROLEY_REQUEST (the table name is spelled this way in the live database and the base scripts).
/// Column names verified against the live FLOWVISION2_TEST schema and db_FlowVision_2.0.35.sql.
/// ROWVER is the rowversion added by migration 2.0.36.
/// </summary>
[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Property names match the shared TROLEY_REQUEST columns.")]
public sealed class TrolleyRequest
{
    public int TRREQ_ID { get; set; }
    public int? TRREQ_REQ_ID { get; set; }
    public int? TRRER_TR_ID { get; set; }
    public DateTime? TR_WORK_START { get; set; }
    public DateTime? TR_WORK_STOP { get; set; }
    public string TRREQ_STATUS { get; set; } = "N";
    public int TRREQ_QUANTITY_GOOD { get; set; }
    public int TRREQ_QUANTITY_BAD { get; set; }
    public byte[] ROWVER { get; set; } = [];
}

/// <summary>EF Core mapping for <see cref="TrolleyRequest"/>. Applied by ShopFloorDbContext (T063).</summary>
public sealed class TrolleyRequestConfiguration : IEntityTypeConfiguration<TrolleyRequest>
{
    public void Configure(EntityTypeBuilder<TrolleyRequest> builder)
    {
        builder.ToTable("TROLEY_REQUEST");
        builder.HasKey(t => t.TRREQ_ID);
        builder.Property(t => t.TRREQ_STATUS).HasMaxLength(1).IsFixedLength().IsRequired();
        builder.Property(t => t.ROWVER).IsRowVersion();
    }
}
