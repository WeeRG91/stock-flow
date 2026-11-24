using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using StockFlow.Domain.Entities;
using StockFlow.Infrastructure.Persistence;

namespace StockFlow.Infrastructure.Tests;

public sealed class TransferModelTests
{
    [Fact]
    public void Model_MapsTransferReceiptsAndProtectedLines()
    {
        var options = new DbContextOptionsBuilder<StockFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=stockflow_model_test")
            .Options;

        using var context = new StockFlowDbContext(options);

        var receipt = context.Model.FindEntityType(typeof(TransferReceipt));
        Assert.NotNull(receipt);
        Assert.Equal("TransferReceipts", receipt.GetTableName());

        var id = receipt.FindProperty(nameof(TransferReceipt.Id));
        Assert.NotNull(id);
        Assert.Equal(ValueGenerated.Never, id.ValueGenerated);

        var navigation = receipt.FindNavigation(nameof(TransferReceipt.Lines));
        Assert.NotNull(navigation);
        Assert.Equal("_lines", navigation.FieldInfo?.Name);
        Assert.Equal(PropertyAccessMode.Field, navigation.GetPropertyAccessMode());

        var line = context.Model.FindEntityType(typeof(TransferReceiptLine));
        Assert.NotNull(line);
        Assert.Equal("TransferReceiptLines", line.GetTableName());

        var key = line.FindPrimaryKey();
        Assert.NotNull(key);
        Assert.Equal(
            new[] { "TransferReceiptId", nameof(TransferReceiptLine.TransferLineId) },
            key.Properties.Select(property => property.Name).ToArray());

        var receiptId = line.FindProperty("TransferReceiptId");
        Assert.NotNull(receiptId);
        Assert.True(receiptId.IsShadowProperty());
        Assert.False(receiptId.IsNullable);

        Assert.Contains(receipt.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Transfer));
        Assert.Contains(line.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(TransferReceipt));
        Assert.Contains(line.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(TransferLine));

        Assert.All(receipt.GetForeignKeys(), foreignKey =>
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        Assert.All(line.GetForeignKeys(), foreignKey =>
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }

    [Fact]
    public void Model_MapsTransferConcurrencyAndProtectedLines()
    {
        var options = new DbContextOptionsBuilder<StockFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=stockflow_model_test")
            .Options;

        using var context = new StockFlowDbContext(options);

        var transfer = context.Model.FindEntityType(typeof(Transfer));
        Assert.NotNull(transfer);
        Assert.Equal("Transfers", transfer.GetTableName());

        var version = transfer.FindProperty("Version");
        Assert.NotNull(version);
        Assert.True(version.IsConcurrencyToken);
        Assert.True(version.IsShadowProperty());
        Assert.Equal(typeof(Guid), version.ClrType);

        Assert.Null(transfer.FindNavigation(nameof(Transfer.PendingReceipts)));

        var lines = transfer.FindNavigation(nameof(Transfer.Lines));
        Assert.NotNull(lines);
        Assert.Equal("_lines", lines.FieldInfo?.Name);

        var line = context.Model.FindEntityType(typeof(TransferLine));
        Assert.NotNull(line);
        Assert.Equal("TransferLines", line.GetTableName());
        Assert.Null(line.FindProperty(nameof(TransferLine.InTransitQuantity)));

        var productIndex = Assert.Single(line.GetIndexes(), index => index.IsUnique);
        Assert.Equal(
            new[] { "TransferId", nameof(TransferLine.ProductId) },
            productIndex.Properties.Select(property => property.Name).ToArray());

        Assert.All(
            transfer.GetForeignKeys(),
            key => Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior));

        Assert.All(
            line.GetForeignKeys(),
            key => Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior));
    }
}
