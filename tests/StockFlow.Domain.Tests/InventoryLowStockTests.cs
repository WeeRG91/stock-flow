using StockFlow.Domain.Entities;

namespace StockFlow.Domain.Tests;

public class InventoryLowStockTests
{
    private static readonly DateTimeOffset OccurredAt =
        new(2026, 10, 16, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NewInventory_HasMonitoringDisabled()
    {
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(inventory.ReorderThreshold);
        Assert.False(inventory.IsLowStock);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SetReorderThreshold_WithInvalidValue_PreservesThreshold(
        int threshold)
    {
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());
        inventory.SetReorderThreshold(10);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => inventory.SetReorderThreshold(threshold));

        Assert.Equal("threshold", exception.ParamName);
        Assert.Equal(10, inventory.ReorderThreshold);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(9, true)]
    [InlineData(10, false)]
    [InlineData(11, false)]
    public void IsLowStock_ComparesQuantityWithThreshold(
        int quantity,
        bool expected)
    {
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());
        inventory.SetReorderThreshold(10);

        if (quantity > 0)
        {
            inventory.ReceiveStock(quantity, "Test receipt", OccurredAt);
        }

        Assert.Equal(expected, inventory.IsLowStock);
    }

    [Fact]
    public void DisableLowStockMonitoring_ClearsThreshold()
    {
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());
        inventory.SetReorderThreshold(10);

        inventory.DisableLowStockMonitoring();

        Assert.Null(inventory.ReorderThreshold);
        Assert.False(inventory.IsLowStock);
    }

    [Fact]
    public void IsLowStock_ReflectsSubsequentStockChanges()
    {
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());
        inventory.SetReorderThreshold(10);
        Assert.True(inventory.IsLowStock);

        inventory.ReceiveStock(10, "Test receipt", OccurredAt);
        Assert.False(inventory.IsLowStock);

        inventory.RemoveStock(1, "Test removal", OccurredAt);
        Assert.True(inventory.IsLowStock);
    }

    [Fact]
    public void ChangingMonitoring_PreservesStockAndPendingMovements()
    {
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());
        inventory.ReceiveStock(5, "Test receipt", OccurredAt);
        var movementsBefore = inventory.PendingMovements.ToArray();

        inventory.SetReorderThreshold(10);

        Assert.Equal(5, inventory.QuantityOnHand);
        Assert.Equal(movementsBefore, inventory.PendingMovements.ToArray());

        inventory.DisableLowStockMonitoring();

        Assert.Equal(5, inventory.QuantityOnHand);
        Assert.Equal(movementsBefore, inventory.PendingMovements.ToArray());
    }
}