using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShopFloor.Data.Entities;

/// <summary>
/// Row of QRCODE: a printed code (barcode text) that the scanner reads. Mapped to the live FLOWVISION2_TEST schema.
/// Since 2.0.35 the text column is QR_BARCODE varchar(64); the older QR_QRCODE varchar(128) no longer exists.
/// </summary>
[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Property names match the shared QRCODE columns.")]
public sealed class QrCode
{
    public int QR_ID { get; set; }
    public string QR_BARCODE { get; set; } = "";
    public string QR_STATUS { get; set; } = "A";
    public byte[] ROWVER { get; set; } = [];
}

/// <summary>
/// Row of TROLLEY_QRCODE: links a QR code to a trolley request. The live table is TROLLEY_QRCODE (TRQR_*);
/// TROLLEY_QRCODES does not exist in the live database (open-questions.md, confirmed).
/// </summary>
[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Property names match the shared TROLLEY_QRCODE columns.")]
public sealed class TrolleyQrCode
{
    public int TRQR_ID { get; set; }
    public int TRQR_TR_ID { get; set; }
    public int TRQR_QR_ID { get; set; }
    public string? TRQR_STATUS { get; set; }
    public int? TRQR_NEXT_TROLLEY_ID { get; set; }
    public byte[] ROWVER { get; set; } = [];
}

/// <summary>EF Core mapping for <see cref="QrCode"/>. Applied by ShopFloorDbContext (T063).</summary>
public sealed class QrCodeConfiguration : IEntityTypeConfiguration<QrCode>
{
    public void Configure(EntityTypeBuilder<QrCode> builder)
    {
        builder.ToTable("QRCODE");
        builder.HasKey(q => q.QR_ID);
        builder.Property(q => q.QR_BARCODE).HasMaxLength(64).IsRequired();
        builder.Property(q => q.QR_STATUS).HasMaxLength(1).IsFixedLength().IsRequired();
        builder.Property(q => q.ROWVER).IsRowVersion();
        builder.HasIndex(q => q.QR_BARCODE).IsUnique().HasDatabaseName("QRCODE_BARCODE");
    }
}

/// <summary>EF Core mapping for <see cref="TrolleyQrCode"/>. Applied by ShopFloorDbContext (T063).</summary>
public sealed class TrolleyQrCodeConfiguration : IEntityTypeConfiguration<TrolleyQrCode>
{
    public void Configure(EntityTypeBuilder<TrolleyQrCode> builder)
    {
        builder.ToTable("TROLLEY_QRCODE");
        builder.HasKey(t => t.TRQR_ID);
        builder.Property(t => t.TRQR_STATUS).HasMaxLength(1).IsFixedLength();
        builder.Property(t => t.ROWVER).IsRowVersion();
    }
}
