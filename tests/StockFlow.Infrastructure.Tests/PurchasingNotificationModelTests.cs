using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using StockFlow.Domain.Entities;
using StockFlow.Infrastructure.Persistence;

namespace StockFlow.Infrastructure.Tests;

public sealed class PurchasingNotificationModelTests
{
    [Fact]
    public void Model_MapsNotificationUniquenessAndReadRules()
    {
        var options = new DbContextOptionsBuilder<StockFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=stockflow_model_test")
            .Options;

        using var context = new StockFlowDbContext(options);
        var model = context.GetService<IDesignTimeModel>().Model;
        var entity = model.FindEntityType(typeof(PurchasingNotification));
        Assert.NotNull(entity);

        Assert.Equal("PurchasingNotifications", entity.GetTableName());
        Assert.True(entity.FindProperty(nameof(PurchasingNotification.ReadAt))!.IsNullable);

        var uniqueIndex = Assert.Single(entity.GetIndexes(), index => index.IsUnique);
        Assert.Equal(
            new[]
            {
                nameof(PurchasingNotification.RecipientUserId),
                nameof(PurchasingNotification.PurchaseOrderId),
                nameof(PurchasingNotification.Kind)
            },
            uniqueIndex.Properties.Select(property => property.Name).ToArray());

        var orderForeignKey = Assert.Single(entity.GetForeignKeys());
        Assert.Equal(typeof(PurchaseOrder), orderForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, orderForeignKey.DeleteBehavior);

        var readConstraint = Assert.Single(entity.GetCheckConstraints());
        Assert.Equal("CK_PurchasingNotifications_ReadAfterCreation", readConstraint.Name);
        Assert.Equal(
            "\"ReadAt\" IS NULL OR \"ReadAt\" >= \"CreatedAt\"",
            readConstraint.Sql);
    }
}