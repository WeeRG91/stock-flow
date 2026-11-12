using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using StockFlow.Domain.Entities;
using StockFlow.Infrastructure.Persistence;

namespace StockFlow.Infrastructure.Tests;

public class PurchaseOrderModelTests
{
    private static StockFlowDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<StockFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=stockflow_model_test")
            .Options;

        return new StockFlowDbContext(options);
    }

    [Fact]
    public void PurchaseOrder_HasUniqueNumberAndApplicationManagedVersion()
    {
        using var context = CreateContext();
        var order = context.Model.FindEntityType(typeof(PurchaseOrder))!;

        var numberIndex = Assert.Single(
            order.GetIndexes(),
            index => index.GetDatabaseName() == "IX_PurchaseOrders_OrderNumber");

        Assert.True(numberIndex.IsUnique);
        Assert.Equal(
            nameof(PurchaseOrder.OrderNumber),
            Assert.Single(numberIndex.Properties).Name);

        var version = order.FindProperty("Version");
        Assert.NotNull(version);
        Assert.Equal(typeof(Guid), version.ClrType);
        Assert.True(version.IsShadowProperty());
        Assert.True(version.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.Never, version.ValueGenerated);
    }

    [Fact]
    public void PurchaseOrder_UsesPrivateLineCollectionAndRestrictiveForeignKeys()
    {
        using var context = CreateContext();
        var order = context.Model.FindEntityType(typeof(PurchaseOrder))!;
        var line = context.Model.FindEntityType(typeof(PurchaseOrderLine))!;

        var navigation = order.FindNavigation(nameof(PurchaseOrder.Lines));
        Assert.NotNull(navigation);
        Assert.Equal("_lines", navigation.FieldInfo?.Name);
        Assert.Equal(PropertyAccessMode.Field, navigation.GetPropertyAccessMode());

        AssertRestrictedForeignKey(order, typeof(Supplier), nameof(PurchaseOrder.SupplierId));
        AssertRestrictedForeignKey(order, typeof(Warehouse), nameof(PurchaseOrder.WarehouseId));
        AssertRestrictedForeignKey(line, typeof(PurchaseOrder), "PurchaseOrderId");
        AssertRestrictedForeignKey(line, typeof(Product), nameof(PurchaseOrderLine.ProductId));
    }

    [Fact]
    public void PurchaseOrderLine_HasUniqueProductWithinOrder()
    {
        using var context = CreateContext();
        var line = context.Model.FindEntityType(typeof(PurchaseOrderLine))!;

        var index = Assert.Single(
            line.GetIndexes(),
            candidate => candidate.GetDatabaseName() ==
                "IX_PurchaseOrderLines_PurchaseOrderId_ProductId");

        Assert.True(index.IsUnique);
        Assert.Equal(
            new[] { "PurchaseOrderId", nameof(PurchaseOrderLine.ProductId) },
            index.Properties.Select(property => property.Name).ToArray());
    }

    [Fact]
    public void PurchaseOrderLine_ConfiguresQuantityAndPriceChecks()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var line = model.FindEntityType(typeof(PurchaseOrderLine))!;

        var quantityCheck = Assert.Single(
            line.GetCheckConstraints(),
            check => check.Name == "CK_PurchaseOrderLines_OrderedQuantity_Positive");

        var priceCheck = Assert.Single(
            line.GetCheckConstraints(),
            check => check.Name == "CK_PurchaseOrderLines_UnitPrice_Valid");

        Assert.Equal("\"OrderedQuantity\" > 0", quantityCheck.Sql);
        Assert.Contains("\"UnitPrice\" >= 0", priceCheck.Sql);
        Assert.Contains("round(\"UnitPrice\", 2)", priceCheck.Sql);
        Assert.Equal(
            "numeric",
            line.FindProperty(nameof(PurchaseOrderLine.UnitPrice))!.GetColumnType());
    }

    private static void AssertRestrictedForeignKey(
        IEntityType dependent,
        Type principalType,
        string propertyName)
    {
        var foreignKey = Assert.Single(
            dependent.GetForeignKeys(),
            candidate => candidate.PrincipalEntityType.ClrType == principalType);

        Assert.True(foreignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(propertyName, Assert.Single(foreignKey.Properties).Name);
    }
}