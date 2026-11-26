using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using StockFlow.Infrastructure.Persistence;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Tests;

public sealed class StockReservationModelTests
{
    [Fact]
    public void Model_MapsReservationIdentityAndAuditFields()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(StockReservation));
        Assert.NotNull(entity);
        Assert.Equal("StockReservations", entity.GetTableName());
        Assert.Equal(new[] { nameof(StockReservation.Id) },
            entity.FindPrimaryKey()!.Properties.Select(property => property.Name).ToArray());
        var id = entity.FindProperty(nameof(StockReservation.Id))!;
        Assert.Equal(ValueGenerated.Never, id.ValueGenerated);
        Assert.Equal("<Id>k__BackingField", id.FieldInfo?.Name);
        Assert.Equal(PropertyAccessMode.Field, id.GetPropertyAccessMode());

        foreach (var name in new[]
        {
            nameof(StockReservation.InventoryId), nameof(StockReservation.Quantity),
            nameof(StockReservation.Reference), nameof(StockReservation.CreatedAt),
            nameof(StockReservation.CreatedByUserId), nameof(StockReservation.Status)
        })
        {
            var property = entity.FindProperty(name);
            Assert.NotNull(property);
            Assert.False(property.IsNullable);
        }

        foreach (var name in new[]
        {
            nameof(StockReservation.ReleasedAt), nameof(StockReservation.ReleasedByUserId),
            nameof(StockReservation.FulfilledAt), nameof(StockReservation.FulfilledByUserId)
        })
        {
            var property = entity.FindProperty(name);
            Assert.NotNull(property);
            Assert.True(property.IsNullable);
        }

        var status = entity.FindProperty(nameof(StockReservation.Status))!;
        var converter = status.GetTypeMapping().Converter;
        Assert.NotNull(converter);
        Assert.Equal(typeof(int), converter.ProviderClrType);
        Assert.Equal(1, converter.ConvertToProvider(StockReservationStatus.Active));
        Assert.Equal(2, converter.ConvertToProvider(StockReservationStatus.Released));
        Assert.Equal(3, converter.ConvertToProvider(StockReservationStatus.Fulfilled));
    }

    [Fact]
    public void Model_MapsUniqueRequestAndApplicationManagedConcurrency()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(StockReservation))!;
        foreach (var name in new[] { "RequestId", "Version" })
        {
            var property = entity.FindProperty(name);
            Assert.NotNull(property);
            Assert.True(property.IsShadowProperty());
            Assert.False(property.IsNullable);
            Assert.Equal(typeof(Guid), property.ClrType);
            Assert.Equal(ValueGenerated.Never, property.ValueGenerated);
        }
        Assert.True(entity.FindProperty("Version")!.IsConcurrencyToken);
        var index = Assert.Single(entity.GetIndexes(), item => item.IsUnique);
        Assert.Equal(new[] { "RequestId" }, index.Properties.Select(property => property.Name).ToArray());
        Assert.Equal("IX_StockReservations_RequestId", index.GetDatabaseName());
    }

    [Fact]
    public void Model_RestrictsInventoryDeletionAndIndexesInventoryStatus()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(StockReservation))!;
        var relationship = Assert.Single(entity.GetForeignKeys());
        Assert.Equal(typeof(InventoryEntity), relationship.PrincipalEntityType.ClrType);
        Assert.Equal(new[] { nameof(StockReservation.InventoryId) },
            relationship.Properties.Select(property => property.Name).ToArray());
        Assert.True(relationship.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior);
        Assert.Null(relationship.DependentToPrincipal);
        Assert.Null(relationship.PrincipalToDependent);
        Assert.Contains(entity.GetIndexes(), index =>
            !index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { nameof(StockReservation.InventoryId), nameof(StockReservation.Status) }));
    }

    [Fact]
    public void Model_MapsReservedBackingFieldAndPreservesInventoryConcurrency()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(InventoryEntity))!;
        var reserved = entity.FindProperty(nameof(InventoryEntity.QuantityReserved));
        Assert.NotNull(reserved);
        Assert.Equal("_quantityReserved", reserved.FieldInfo?.Name);
        Assert.Equal(PropertyAccessMode.Field, reserved.GetPropertyAccessMode());
        Assert.False(reserved.IsNullable);
        Assert.Equal(0, reserved.GetDefaultValue());
        Assert.Null(entity.FindProperty(nameof(InventoryEntity.QuantityAvailable)));
        Assert.Null(entity.FindNavigation(nameof(InventoryEntity.PendingMovements)));
        var version = entity.FindProperty("Version")!;
        Assert.True(version.IsConcurrencyToken);
        Assert.Equal(typeof(uint), version.ClrType);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, version.ValueGenerated);

        var designEntity = context.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(InventoryEntity))!;
        var constraint = Assert.Single(designEntity.GetCheckConstraints(),
            item => item.Name == "CK_Inventories_QuantityReserved_Valid");
        Assert.Equal("\"QuantityReserved\" >= 0 AND \"QuantityReserved\" <= \"QuantityOnHand\"",
            constraint.Sql);
    }

    [Fact]
    public void Model_DeclaresReservationChecks()
    {
        using var context = CreateContext();
        var entity = context.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(StockReservation))!;
        var constraints = entity.GetCheckConstraints().ToDictionary(item => item.Name);
        Assert.Equal("\"Quantity\" > 0", constraints["CK_StockReservations_Quantity_Positive"].Sql);
        Assert.Equal("\"RequestId\" <> '00000000-0000-0000-0000-000000000000'::uuid",
            constraints["CK_StockReservations_RequestId_NotEmpty"].Sql);
        var lifecycle = constraints["CK_StockReservations_StatusAndHistory"].Sql;
        foreach (var fragment in new[]
        {
            "\"Status\" = 1", "\"Status\" = 2", "\"Status\" = 3",
            "\"ReleasedAt\" IS NULL", "\"ReleasedAt\" IS NOT NULL",
            "\"FulfilledAt\" IS NULL", "\"FulfilledAt\" IS NOT NULL",
            "\"ReleasedAt\" >= \"CreatedAt\"", "\"FulfilledAt\" >= \"CreatedAt\"",
            "\"ReleasedByUserId\" IS NOT NULL", "\"FulfilledByUserId\" IS NOT NULL"
        })
        {
            Assert.Contains(fragment, lifecycle);
        }
    }

    [Fact]
    public void Tracking_ReadsAndWritesReservedFieldAndDetectsDomainChanges()
    {
        using var context = CreateContext();
        var inventory = new InventoryEntity(Guid.NewGuid(), Guid.NewGuid());
        inventory.ReceiveStock(100, "Synthetic stock", new DateTimeOffset(2026, 11, 26, 10, 0, 0, TimeSpan.Zero));
        inventory.AcknowledgePersistedMovements(inventory.PendingMovements.Select(item => item.Id).ToArray());
        context.Attach(inventory);
        var entry = context.Entry(inventory);
        entry.Property(item => item.QuantityReserved).CurrentValue = 30;
        Assert.Equal(30, inventory.QuantityReserved);
        Assert.Equal(70, inventory.QuantityAvailable);
        // Accept the seeded tracked values as the baseline without saving to a database.
        context.ChangeTracker.AcceptAllChanges();

        inventory.ReserveStock(10);
        context.ChangeTracker.DetectChanges();

        Assert.True(entry.Property(item => item.QuantityReserved).IsModified);
        Assert.False(entry.Property(item => item.QuantityOnHand).IsModified);
        Assert.Equal(40, entry.Property(item => item.QuantityReserved).CurrentValue);
        Assert.Equal(30, entry.Property(item => item.QuantityReserved).OriginalValue);
        Assert.Equal(60, inventory.QuantityAvailable);
        Assert.Empty(inventory.PendingMovements);
    }

    private static StockFlowDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<StockFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=stockflow_model_test")
            .Options;
        return new StockFlowDbContext(options);
    }
}
