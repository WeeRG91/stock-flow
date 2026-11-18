using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using StockFlow.Domain.Entities;
using StockFlow.Infrastructure.Persistence;

namespace StockFlow.Infrastructure.Tests;

public sealed class OverduePurchaseOrderAlertModelTests
{
    [Fact]
    public void Model_MapsOptionalDeliveryDateAndOneAlertPerOrder()
    {
        var options = new DbContextOptionsBuilder<StockFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=stockflow_model_test")
            .Options;

        using var context = new StockFlowDbContext(options);
        var model = context.GetService<IDesignTimeModel>().Model;

        var order = model.FindEntityType(typeof(PurchaseOrder));
        Assert.NotNull(order);
        var deliveryDate = order.FindProperty(nameof(PurchaseOrder.ExpectedDeliveryDate));
        Assert.NotNull(deliveryDate);
        Assert.True(deliveryDate.IsNullable);
        Assert.Equal("date", deliveryDate.GetColumnType());

        var alert = model.FindEntityType(typeof(OverduePurchaseOrderAlert));
        Assert.NotNull(alert);
        Assert.Equal("OverduePurchaseOrderAlerts", alert.GetTableName());

        var orderIndex = Assert.Single(alert.GetIndexes(), index => index.IsUnique);
        Assert.Equal(
            nameof(OverduePurchaseOrderAlert.PurchaseOrderId),
            Assert.Single(orderIndex.Properties).Name);

        var foreignKey = Assert.Single(alert.GetForeignKeys());
        Assert.Equal(typeof(PurchaseOrder), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);

        var constraint = Assert.Single(alert.GetCheckConstraints());
        Assert.Equal(
            "\"ResolvedAt\" IS NULL OR \"ResolvedAt\" >= \"DetectedAt\"",
            constraint.Sql);
    }
}