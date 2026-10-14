using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.Domain.Tests;

public class InventoryTests
{
    private static readonly DateTimeOffset OccurredAt =
        new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WithValidIdentifiers_CreatesEmptyInventory()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        // Act
        var inventory = new Inventory(productId, warehouseId);

        // Assert
        Assert.NotEqual(Guid.Empty, inventory.Id);
        Assert.Equal(productId, inventory.ProductId);
        Assert.Equal(warehouseId, inventory.WarehouseId);
        Assert.Equal(0, inventory.QuantityOnHand);
    }

    [Fact]
    public void Constructor_WithEmptyProductId_ThrowsArgumentException()
    {
        // Arrange
        var warehouseId = Guid.NewGuid();

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => new Inventory(Guid.Empty, warehouseId));

        // Assert
        Assert.Equal("productId", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithEmptyWarehouseId_ThrowsArgumentException()
    {
        // Arrange
        var productId = Guid.NewGuid();

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => new Inventory(productId, Guid.Empty));

        // Assert
        Assert.Equal("warehouseId", exception.ParamName);
    }

    [Fact]
    public void ReceiveStock_WithPositiveQuantity_IncreasesQuantityOnHand()
    {
        // Arrange
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());
        inventory.ReceiveStock(10, "Test receipt", OccurredAt);

        // Act
        inventory.ReceiveStock(5, "Test receipt", OccurredAt);

        // Assert
        Assert.Equal(15, inventory.QuantityOnHand);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReceiveStock_WithNonPositiveQuantity_ThrowsAndPreservesStock(
    int quantity)
    {
        // Arrange
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());
        inventory.ReceiveStock(10, "Test receipt", OccurredAt);

        var movementsBefore = inventory.PendingMovements.ToArray();

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => inventory.ReceiveStock(quantity, "Test receipt", OccurredAt));

        // Assert
        Assert.Equal("quantity", exception.ParamName);
        Assert.Equal(10, inventory.QuantityOnHand);
        Assert.Equal(movementsBefore, inventory.PendingMovements.ToArray());
    }

    [Fact]
    public void ReceiveStock_WhenAdditionOverflows_ThrowsAndPreservesStock()
    {
        // Arrange
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());
        inventory.ReceiveStock(int.MaxValue, "Test receipt", OccurredAt);

        var movementsBefore = inventory.PendingMovements.ToArray();

        // Act
        Assert.Throws<OverflowException>(
            () => inventory.ReceiveStock(1, "Test receipt", OccurredAt));

        // Assert
        Assert.Equal(int.MaxValue, inventory.QuantityOnHand);
        Assert.Equal(movementsBefore, inventory.PendingMovements.ToArray());
    }

    [Fact]
    public void RemoveStock_WithAvailableQuantity_DecreasesQuantityOnHand()
    {
        // Arrange
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());
        inventory.ReceiveStock(10, "Test receipt", OccurredAt);

        // Act
        inventory.RemoveStock(4, "Test removal", OccurredAt);

        // Assert
        Assert.Equal(6, inventory.QuantityOnHand);
    }

    [Fact]
    public void RemoveStock_WithExactAvailableQuantity_LeavesZeroStock()
    {
        // Arrange
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());
        inventory.ReceiveStock(10, "Test receipt", OccurredAt);

        // Act
        inventory.RemoveStock(10, "Test removal", OccurredAt);

        // Assert
        Assert.Equal(0, inventory.QuantityOnHand);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RemoveStock_WithNonPositiveQuantity_ThrowsAndPreservesStock(
    int quantity)
    {
        // Arrange
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());
        inventory.ReceiveStock(10, "Test receipt", OccurredAt);

        var movementsBefore = inventory.PendingMovements.ToArray();

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => inventory.RemoveStock(quantity, "Test removal", OccurredAt));

        // Assert
        Assert.Equal("quantity", exception.ParamName);
        Assert.Equal(10, inventory.QuantityOnHand);
        Assert.Equal(movementsBefore, inventory.PendingMovements.ToArray());
    }

    [Theory]
    [InlineData(10, 11)]
    [InlineData(0, 1)]
    public void RemoveStock_WithInsufficientStock_ThrowsAndPreservesStock(
    int availableQuantity,
    int quantityToRemove)
    {
        // Arrange
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());

        if (availableQuantity > 0)
        {
            inventory.ReceiveStock(availableQuantity, "Test receipt", OccurredAt);
        }

        var movementsBefore = inventory.PendingMovements.ToArray();

        // Act
        Assert.Throws<InvalidOperationException>(
            () => inventory.RemoveStock(quantityToRemove, "Test removal", OccurredAt));

        // Assert
        Assert.Equal(availableQuantity, inventory.QuantityOnHand);
        Assert.Equal(movementsBefore, inventory.PendingMovements.ToArray());
    }

    [Fact]
    public void ReceiveStock_WithValidInput_CreatesReceiptMovement()
    {
        // Arrange
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());

        // Act
        inventory.ReceiveStock(10, "Goods received", OccurredAt);

        // Assert
        Assert.Equal(10, inventory.QuantityOnHand);

        var movement = Assert.Single(inventory.PendingMovements);

        Assert.NotEqual(Guid.Empty, movement.Id);
        Assert.Equal(inventory.Id, movement.InventoryId);
        Assert.Equal(10, movement.QuantityChange);
        Assert.Equal(StockMovementType.Receipt, movement.Type);
        Assert.Equal("Goods received", movement.Reason);
        Assert.Equal(OccurredAt, movement.OccurredAt);
    }

    [Fact]
    public void RemoveStock_WithValidInput_CreatesRemovalMovement()
    {
        // Arrange
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());
        inventory.ReceiveStock(10, "Initial stock", OccurredAt);
        var removalTime = OccurredAt.AddMinutes(5);

        // Act
        inventory.RemoveStock(4, "Customer shipment", removalTime);

        // Assert
        Assert.Equal(6, inventory.QuantityOnHand);
        Assert.Equal(2, inventory.PendingMovements.Count);

        var movement = inventory.PendingMovements[1];

        Assert.NotEqual(Guid.Empty, movement.Id);
        Assert.Equal(inventory.Id, movement.InventoryId);
        Assert.Equal(-4, movement.QuantityChange);
        Assert.Equal(StockMovementType.Removal, movement.Type);
        Assert.Equal("Customer shipment", movement.Reason);
        Assert.Equal(removalTime, movement.OccurredAt);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(true, "   ")]
    [InlineData(true, "\t\r\n")]
    [InlineData(false, null)]
    [InlineData(false, "")]
    [InlineData(false, "   ")]
    [InlineData(false, "\t\r\n")]
    public void StockOperation_WithInvalidReason_PreservesState(
    bool isReceipt,
    string? reason)
    {
        // Arrange
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());
        inventory.ReceiveStock(10, "Initial stock", OccurredAt);
        var movementsBefore = inventory.PendingMovements.ToArray();

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            if (isReceipt)
            {
                inventory.ReceiveStock(1, reason!, OccurredAt);
            }
            else
            {
                inventory.RemoveStock(1, reason!, OccurredAt);
            }
        });

        // Assert
        Assert.Equal("reason", exception.ParamName);
        Assert.Equal(10, inventory.QuantityOnHand);
        Assert.Equal(movementsBefore, inventory.PendingMovements.ToArray());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StockOperation_WithDefaultTimestamp_PreservesState(
        bool isReceipt)
    {
        // Arrange
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());
        inventory.ReceiveStock(10, "Initial stock", OccurredAt);
        var movementsBefore = inventory.PendingMovements.ToArray();

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            if (isReceipt)
            {
                inventory.ReceiveStock(1, "Receipt", default);
            }
            else
            {
                inventory.RemoveStock(1, "Removal", default);
            }
        });

        // Assert
        Assert.Equal("occurredAt", exception.ParamName);
        Assert.Equal(10, inventory.QuantityOnHand);
        Assert.Equal(movementsBefore, inventory.PendingMovements.ToArray());
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(true, -5)]
    [InlineData(false, 2)]
    [InlineData(false, -5)]
    public void StockOperation_WithNonUtcTimestamp_PreservesState(
        bool isReceipt,
        int offsetHours)
    {
        // Arrange
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());
        inventory.ReceiveStock(10, "Initial stock", OccurredAt);
        var movementsBefore = inventory.PendingMovements.ToArray();
        var nonUtcTimestamp = OccurredAt.ToOffset(
            TimeSpan.FromHours(offsetHours));

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            if (isReceipt)
            {
                inventory.ReceiveStock(1, "Receipt", nonUtcTimestamp);
            }
            else
            {
                inventory.RemoveStock(1, "Removal", nonUtcTimestamp);
            }
        });

        // Assert
        Assert.Equal("occurredAt", exception.ParamName);
        Assert.Equal(10, inventory.QuantityOnHand);
        Assert.Equal(movementsBefore, inventory.PendingMovements.ToArray());
    }

    [Fact]
    public void Constructor_CreatesNoPendingMovements()
    {
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());

        Assert.Empty(inventory.PendingMovements);
    }

    [Fact]
    public void StockOperations_CreateOneMovementPerOperation()
    {
        // Arrange
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());

        // Act and assert
        inventory.ReceiveStock(10, "First receipt", OccurredAt);
        Assert.Single(inventory.PendingMovements);

        inventory.ReceiveStock(5, "Second receipt", OccurredAt.AddMinutes(1));
        Assert.Equal(2, inventory.PendingMovements.Count);

        inventory.RemoveStock(15, "Remove all stock", OccurredAt.AddMinutes(2));
        Assert.Equal(3, inventory.PendingMovements.Count);

        Assert.Equal(0, inventory.QuantityOnHand);

        Assert.Collection(
            inventory.PendingMovements,
            movement =>
            {
                Assert.Equal(10, movement.QuantityChange);
                Assert.Equal(StockMovementType.Receipt, movement.Type);
            },
            movement =>
            {
                Assert.Equal(5, movement.QuantityChange);
                Assert.Equal(StockMovementType.Receipt, movement.Type);
            },
            movement =>
            {
                Assert.Equal(-15, movement.QuantityChange);
                Assert.Equal(StockMovementType.Removal, movement.Type);
            });

        Assert.All(
            inventory.PendingMovements,
            movement => Assert.Equal(inventory.Id, movement.InventoryId));

        Assert.Equal(
            3,
            inventory.PendingMovements.Select(m => m.Id).Distinct().Count());
    }

    [Fact]
    public void PendingMovements_RejectsExternalCollectionChanges()
    {
        // Arrange
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());
        inventory.ReceiveStock(10, "Initial stock", OccurredAt);

        var movementsBefore = inventory.PendingMovements.ToArray();
        var collection = Assert.IsAssignableFrom<IList<StockMovement>>(
            inventory.PendingMovements);

        var extraMovement = new StockMovement(
            inventory.Id,
            1,
            StockMovementType.Receipt,
            "External addition",
            OccurredAt);

        // Act and assert
        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(
            () => collection.Add(extraMovement));
        Assert.Throws<NotSupportedException>(
            () => collection.RemoveAt(0));
        Assert.Throws<NotSupportedException>(
            () => collection[0] = extraMovement);
        Assert.Throws<NotSupportedException>(
            () => collection.Clear());

        Assert.Equal(10, inventory.QuantityOnHand);
        Assert.Equal(movementsBefore, inventory.PendingMovements.ToArray());
    }
}