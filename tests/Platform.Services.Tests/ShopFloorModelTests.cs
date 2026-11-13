using Microsoft.EntityFrameworkCore;
using ShopFloor.Data;
using ShopFloor.Data.Entities;
using Xunit;

namespace Platform.Services.Tests;

/// <summary>T063: the ShopFloor model maps each table to its live name and checks ROWVER on every write. No database connection is opened.</summary>
public class ShopFloorModelTests
{
    private static ShopFloorDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ShopFloorDbContext>()
            .UseSqlServer("Server=localhost;Database=FLOWVISION2_TEST;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new ShopFloorDbContext(options);
    }

    [Theory]
    [InlineData(typeof(TrolleyRequest), "TROLEY_REQUEST")]
    [InlineData(typeof(TrolleyOperation), "TROLLEY_OPERATIONS")]
    [InlineData(typeof(TrolleyQrCode), "TROLLEY_QRCODE")]
    [InlineData(typeof(QrCode), "QRCODE")]
    [InlineData(typeof(WorkRecord), "TIME_OF_OPERATIONS")]
    [InlineData(typeof(TimeOfOperationQrCode), "TIME_OF_OPERATIONS_QRCODES")]
    [InlineData(typeof(RequestOperation), "REQUEST_OPERATIONS")]
    public void EachEntity_MapsToLiveTableName(Type entityType, string tableName)
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(entityType);

        Assert.NotNull(entity);
        Assert.Equal(tableName, entity!.GetTableName());
    }

    [Theory]
    [InlineData(typeof(TrolleyRequest))]
    [InlineData(typeof(TrolleyOperation))]
    [InlineData(typeof(TrolleyQrCode))]
    [InlineData(typeof(QrCode))]
    [InlineData(typeof(WorkRecord))]
    [InlineData(typeof(TimeOfOperationQrCode))]
    [InlineData(typeof(RequestOperation))]
    public void EachEntity_ChecksRowVersionOnWrite(Type entityType)
    {
        using var context = CreateContext();

        var rowVersion = context.Model.FindEntityType(entityType)!.FindProperty("ROWVER");

        Assert.NotNull(rowVersion);
        Assert.True(rowVersion!.IsConcurrencyToken);
    }
}
