using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using StockFlow.Domain.Entities;
using StockFlow.Infrastructure.Persistence;

namespace StockFlow.Infrastructure.Tests;

public sealed class TransferModelTests
{
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