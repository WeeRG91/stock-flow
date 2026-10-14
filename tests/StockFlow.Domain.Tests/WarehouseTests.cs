using StockFlow.Domain.Entities;

namespace StockFlow.Domain.Tests;

public class WarehouseTests
{
    [Fact]
    public void Constructor_WithValidInput_CreatesNormalizedActiveWarehouse()
    {
        var warehouse = new Warehouse(
            "  bru-01  ",
            "  Brussels Warehouse  ",
            "  Brussels, Belgium  ");

        Assert.NotEqual(Guid.Empty, warehouse.Id);
        Assert.Equal("BRU-01", warehouse.Code);
        Assert.Equal("Brussels Warehouse", warehouse.Name);
        Assert.Equal("Brussels, Belgium", warehouse.Location);
        Assert.True(warehouse.IsActive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Constructor_WithInvalidCode_ThrowsArgumentException(
        string? code)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new Warehouse(
                code!,
                "Brussels Warehouse",
                "Brussels, Belgium"));

        Assert.Equal("code", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Constructor_WithInvalidName_ThrowsArgumentException(
        string? name)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new Warehouse(
                "BRU-01",
                name!,
                "Brussels, Belgium"));

        Assert.Equal("name", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Constructor_WithInvalidLocation_ThrowsArgumentException(
        string? location)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new Warehouse(
                "BRU-01",
                "Brussels Warehouse",
                location!));

        Assert.Equal("location", exception.ParamName);
    }

    [Fact]
    public void Deactivate_PreservesWarehouseDetails()
    {
        var warehouse = new Warehouse(
            "BRU-01",
            "Brussels Warehouse",
            "Brussels, Belgium");
        var originalId = warehouse.Id;

        warehouse.Deactivate();

        Assert.False(warehouse.IsActive);
        Assert.Equal(originalId, warehouse.Id);
        Assert.Equal("BRU-01", warehouse.Code);
        Assert.Equal("Brussels Warehouse", warehouse.Name);
        Assert.Equal("Brussels, Belgium", warehouse.Location);
    }

    [Fact]
    public void Activate_AfterDeactivation_PreservesIdentity()
    {
        var warehouse = new Warehouse(
            "BRU-01",
            "Brussels Warehouse",
            "Brussels, Belgium");
        var originalId = warehouse.Id;
        warehouse.Deactivate();

        warehouse.Activate();

        Assert.True(warehouse.IsActive);
        Assert.Equal(originalId, warehouse.Id);
    }

    [Fact]
    public void LifecycleMethods_CanBeCalledRepeatedly()
    {
        var warehouse = new Warehouse(
            "BRU-01",
            "Brussels Warehouse",
            "Brussels, Belgium");

        warehouse.Activate();
        warehouse.Activate();
        Assert.True(warehouse.IsActive);

        warehouse.Deactivate();
        warehouse.Deactivate();
        Assert.False(warehouse.IsActive);

        warehouse.Activate();
        warehouse.Activate();
        Assert.True(warehouse.IsActive);
    }

    [Fact]
    public void Deactivate_PreservesExistingInventoryAndMovements()
    {
        var warehouse = new Warehouse(
            "BRU-01",
            "Brussels Warehouse",
            "Brussels, Belgium");
        var inventory = new Inventory(Guid.NewGuid(), warehouse.Id);
        var occurredAt = new DateTimeOffset(
            2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

        inventory.ReceiveStock(10, "Initial stock", occurredAt);
        var movementsBefore = inventory.PendingMovements.ToArray();

        warehouse.Deactivate();

        Assert.False(warehouse.IsActive);
        Assert.Equal(warehouse.Id, inventory.WarehouseId);
        Assert.Equal(10, inventory.QuantityOnHand);
        Assert.Equal(movementsBefore, inventory.PendingMovements.ToArray());
    }
}