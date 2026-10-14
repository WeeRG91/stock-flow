using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.Domain.Tests;

public class StockMovementTests
{
    [Fact]
    public void Constructor_WithValidReceipt_CreatesMovement()
    {
        // Arrange
        var inventoryId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(
            2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

        // Act
        var movement = new StockMovement(
            inventoryId,
            10,
            StockMovementType.Receipt,
            "Goods received",
            occurredAt);

        // Assert
        Assert.NotEqual(Guid.Empty, movement.Id);
        Assert.Equal(inventoryId, movement.InventoryId);
        Assert.Equal(10, movement.QuantityChange);
        Assert.Equal(StockMovementType.Receipt, movement.Type);
        Assert.Equal("Goods received", movement.Reason);
        Assert.Equal(occurredAt, movement.OccurredAt);
    }

    [Theory]
    [InlineData(StockMovementType.Removal, -10)]
    [InlineData(StockMovementType.Adjustment, 5)]
    [InlineData(StockMovementType.Adjustment, -5)]
    public void Constructor_WithValidMovement_CreatesMovement(
    StockMovementType type,
    int quantityChange)
    {
        // Arrange
        var inventoryId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(
            2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

        // Act
        var movement = new StockMovement(
            inventoryId,
            quantityChange,
            type,
            "Stock correction or removal",
            occurredAt);

        // Assert
        Assert.NotEqual(Guid.Empty, movement.Id);
        Assert.Equal(inventoryId, movement.InventoryId);
        Assert.Equal(quantityChange, movement.QuantityChange);
        Assert.Equal(type, movement.Type);
        Assert.Equal("Stock correction or removal", movement.Reason);
        Assert.Equal(occurredAt, movement.OccurredAt);
    }

    [Fact]
    public void Constructor_WithEmptyInventoryId_ThrowsArgumentException()
    {
        var occurredAt = new DateTimeOffset(
            2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

        var exception = Assert.Throws<ArgumentException>(
            () => new StockMovement(
                Guid.Empty,
                10,
                StockMovementType.Receipt,
                "Goods received",
                occurredAt));

        Assert.Equal("inventoryId", exception.ParamName);
    }

    [Theory]
    [InlineData(StockMovementType.Receipt)]
    [InlineData(StockMovementType.Removal)]
    [InlineData(StockMovementType.Adjustment)]
    public void Constructor_WithZeroQuantity_ThrowsArgumentOutOfRangeException(
        StockMovementType type)
    {
        var inventoryId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(
            2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new StockMovement(
                inventoryId,
                0,
                type,
                "Stock change",
                occurredAt));

        Assert.Equal("quantityChange", exception.ParamName);
    }

    [Theory]
    [InlineData(StockMovementType.Receipt, -1)]
    [InlineData(StockMovementType.Removal, 1)]
    public void Constructor_WithIncorrectQuantitySign_ThrowsArgumentOutOfRangeException(
        StockMovementType type,
        int quantityChange)
    {
        var inventoryId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(
            2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new StockMovement(
                inventoryId,
                quantityChange,
                type,
                "Stock change",
                occurredAt));

        Assert.Equal("quantityChange", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void Constructor_WithUndefinedType_ThrowsArgumentOutOfRangeException(
        int typeValue)
    {
        var inventoryId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(
            2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new StockMovement(
                inventoryId,
                10,
                (StockMovementType)typeValue,
                "Stock change",
                occurredAt));

        Assert.Equal("type", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Constructor_WithInvalidReason_ThrowsArgumentException(
        string? reason)
    {
        var inventoryId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(
            2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

        var exception = Assert.Throws<ArgumentException>(
            () => new StockMovement(
                inventoryId,
                10,
                StockMovementType.Receipt,
                reason!,
                occurredAt));

        Assert.Equal("reason", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithDefaultTimestamp_ThrowsArgumentOutOfRangeException()
    {
        var inventoryId = Guid.NewGuid();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new StockMovement(
                inventoryId,
                10,
                StockMovementType.Receipt,
                "Goods received",
                default(DateTimeOffset)));

        Assert.Equal("occurredAt", exception.ParamName);
    }
}