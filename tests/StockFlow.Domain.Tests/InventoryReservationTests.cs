using StockFlow.Domain.Entities;

namespace StockFlow.Domain.Tests;

public sealed class InventoryReservationTests
{
    [Theory]
    [InlineData(10, 20, 80)]
    [InlineData(30, 0, 100)]
    public void ReleaseReservedStock_RestoresAvailabilityWithoutMovement(
        int quantity, int reserved, int available)
    {
        var inventory = CreateInventory();
        var movements = inventory.PendingMovements.ToArray();
        inventory.ReleaseReservedStock(quantity);
        AssertBalances(inventory, 100, reserved, available);
        Assert.Equal(movements, inventory.PendingMovements.ToArray());
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(true, -1)]
    [InlineData(true, int.MinValue)]
    [InlineData(false, 0)]
    [InlineData(false, -1)]
    [InlineData(false, int.MinValue)]
    public void ReleaseOrConsume_WithNonPositiveQuantity_PreservesState(bool release, int quantity)
    {
        var inventory = CreateInventory();
        var movements = inventory.PendingMovements.ToArray();
        Assert.Throws<ArgumentOutOfRangeException>("quantity", () =>
        {
            if (release) inventory.ReleaseReservedStock(quantity);
            else inventory.ConsumeReservedStock(quantity, "Fulfillment", OccurredAt);
        });
        AssertBalances(inventory, 100, 30, 70);
        Assert.Equal(movements, inventory.PendingMovements.ToArray());
    }

    [Theory]
    [InlineData(true, 0, 1)]
    [InlineData(true, 30, 31)]
    [InlineData(false, 0, 1)]
    [InlineData(false, 30, 31)]
    public void ReleaseOrConsume_WithInsufficientReservation_PreservesState(
        bool release, int reserved, int quantity)
    {
        var inventory = CreateInventory(100, reserved);
        var movements = inventory.PendingMovements.ToArray();
        Assert.Throws<InvalidOperationException>(() =>
        {
            if (release) inventory.ReleaseReservedStock(quantity);
            else inventory.ConsumeReservedStock(quantity, "Fulfillment", OccurredAt);
        });
        AssertBalances(inventory, 100, reserved, 100 - reserved);
        Assert.Equal(movements, inventory.PendingMovements.ToArray());
    }

    [Theory]
    [InlineData(10, 90, 20)]
    [InlineData(30, 70, 0)]
    public void ConsumeReservedStock_UpdatesBothBalancesAndCreatesOneRemoval(
        int quantity, int onHand, int reserved)
    {
        var inventory = CreateInventory();
        var originalMovement = Assert.Single(inventory.PendingMovements);
        var timestamp = OccurredAt.AddMinutes(10);
        inventory.ConsumeReservedStock(quantity, "Reservation fulfilled", timestamp);
        AssertBalances(inventory, onHand, reserved, 70);
        Assert.Equal(2, inventory.PendingMovements.Count);
        Assert.Same(originalMovement, inventory.PendingMovements[0]);
        var movement = inventory.PendingMovements[1];
        Assert.NotEqual(Guid.Empty, movement.Id);
        Assert.NotEqual(originalMovement.Id, movement.Id);
        Assert.Equal(inventory.Id, movement.InventoryId);
        Assert.Equal(-quantity, movement.QuantityChange);
        Assert.Equal(StockFlow.Domain.Enums.StockMovementType.Removal, movement.Type);
        Assert.Equal("Reservation fulfilled", movement.Reason);
        Assert.Equal(timestamp, movement.OccurredAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void ConsumeReservedStock_WithBlankReason_PreservesState(string? reason)
    {
        var inventory = CreateInventory();
        var movements = inventory.PendingMovements.ToArray();
        Assert.Throws<ArgumentException>("reason", () =>
            inventory.ConsumeReservedStock(30, reason!, OccurredAt));
        AssertBalances(inventory, 100, 30, 70);
        Assert.Equal(movements, inventory.PendingMovements.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-5)]
    public void ConsumeReservedStock_WithInvalidTimestamp_PreservesState(int offsetHours)
    {
        var inventory = CreateInventory();
        var movements = inventory.PendingMovements.ToArray();
        var timestamp = offsetHours == 0
            ? default
            : OccurredAt.ToOffset(TimeSpan.FromHours(offsetHours));
        Assert.Throws<ArgumentException>("occurredAt", () =>
            inventory.ConsumeReservedStock(30, "Fulfillment", timestamp));
        AssertBalances(inventory, 100, 30, 70);
        Assert.Equal(movements, inventory.PendingMovements.ToArray());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void ConsumeReservedStock_CanConsumeFullyReservedBoundaryStock(int quantity)
    {
        var inventory = CreateInventory(quantity, quantity);
        inventory.ConsumeReservedStock(quantity, "Fulfillment", OccurredAt);
        AssertBalances(inventory, 0, 0, 0);
        Assert.Equal(2, inventory.PendingMovements.Count);
        Assert.Equal(-quantity, inventory.PendingMovements[1].QuantityChange);
    }

    [Theory]
    [InlineData(30, 71)]
    [InlineData(100, 1)]
    public void RemoveStock_WhenItWouldUseReservedStock_PreservesState(int reserved, int quantity)
    {
        var inventory = CreateInventory(100, reserved);
        var movements = inventory.PendingMovements.ToArray();
        Assert.Throws<InvalidOperationException>(() =>
            inventory.RemoveStock(quantity, "Ordinary removal", OccurredAt));
        AssertBalances(inventory, 100, reserved, 100 - reserved);
        Assert.Equal(movements, inventory.PendingMovements.ToArray());
    }

    [Fact]
    public void RemoveStock_CanRemoveExactlyAvailableStock()
    {
        var inventory = CreateInventory();
        inventory.RemoveStock(70, "Ordinary removal", OccurredAt);
        AssertBalances(inventory, 30, 30, 0);
        Assert.Equal(2, inventory.PendingMovements.Count);
        Assert.Equal(-70, inventory.PendingMovements[1].QuantityChange);
    }

    [Fact]
    public void ReceiveStock_IncreasesAvailabilityAndPreservesReservations()
    {
        var inventory = CreateInventory();
        inventory.ReceiveStock(20, "Additional receipt", OccurredAt);
        AssertBalances(inventory, 120, 30, 90);
        Assert.Equal(2, inventory.PendingMovements.Count);
        Assert.Equal(20, inventory.PendingMovements[1].QuantityChange);
    }

    [Fact]
    public void ReceiveStock_WhenAdditionOverflows_PreservesState()
    {
        var inventory = CreateInventory(int.MaxValue, 30);
        var movements = inventory.PendingMovements.ToArray();
        Assert.Throws<OverflowException>(() =>
            inventory.ReceiveStock(1, "Additional receipt", OccurredAt));
        AssertBalances(inventory, int.MaxValue, 30, int.MaxValue - 30);
        Assert.Equal(movements, inventory.PendingMovements.ToArray());
    }

    [Fact]
    public void ReserveStock_AtIntegerLimit_RejectsAdditionalAllocation()
    {
        var inventory = CreateInventory(int.MaxValue, int.MaxValue);
        var movements = inventory.PendingMovements.ToArray();
        Assert.Throws<InvalidOperationException>(() => inventory.ReserveStock(1));
        AssertBalances(inventory, int.MaxValue, int.MaxValue, 0);
        Assert.Equal(movements, inventory.PendingMovements.ToArray());
    }

    [Fact]
    public void ReserveStock_WithNoStock_RejectsWithoutChanges()
    {
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => inventory.ReserveStock(1));
        AssertBalances(inventory, 0, 0, 0);
        Assert.Empty(inventory.PendingMovements);
    }

    [Fact]
    public void ReleasedAllocation_CanBeReservedAgainWithoutMovement()
    {
        var inventory = CreateInventory();
        var movements = inventory.PendingMovements.ToArray();
        inventory.ReleaseReservedStock(30);
        inventory.ReserveStock(100);
        AssertBalances(inventory, 100, 100, 0);
        Assert.Equal(movements, inventory.PendingMovements.ToArray());
    }

    [Fact]
    public void ReservationBalances_DoNotExposeSetters()
    {
        Assert.Null(typeof(Inventory).GetProperty(nameof(Inventory.QuantityReserved))!.SetMethod);
        Assert.Null(typeof(Inventory).GetProperty(nameof(Inventory.QuantityAvailable))!.SetMethod);
    }

    private static Inventory CreateInventory(int onHand = 100, int reserved = 30)
    {
        var inventory = new Inventory(Guid.NewGuid(), Guid.NewGuid());
        inventory.ReceiveStock(onHand, "Initial stock", OccurredAt);
        if (reserved > 0)
        {
            inventory.ReserveStock(reserved);
        }
        return inventory;
    }

    private static void AssertBalances(Inventory inventory, int onHand, int reserved, int available)
    {
        Assert.Equal(onHand, inventory.QuantityOnHand);
        Assert.Equal(reserved, inventory.QuantityReserved);
        Assert.Equal(available, inventory.QuantityAvailable);
    }

    private static readonly DateTimeOffset OccurredAt =
        new(2026, 11, 25, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_StartsWithoutReservedStock()
    {
        var inventory = new Inventory(
            Guid.NewGuid(),
            Guid.NewGuid());

        Assert.Equal(0, inventory.QuantityOnHand);
        Assert.Equal(0, inventory.QuantityReserved);
        Assert.Equal(0, inventory.QuantityAvailable);
        Assert.Empty(inventory.PendingMovements);
    }

    [Fact]
    public void ReserveStock_AllocatesWithoutPhysicalMovement()
    {
        var inventory = new Inventory(
            Guid.NewGuid(),
            Guid.NewGuid());

        inventory.ReceiveStock(100, "Initial stock", OccurredAt);
        var originalMovement = Assert.Single(inventory.PendingMovements);

        inventory.ReserveStock(30);

        Assert.Equal(100, inventory.QuantityOnHand);
        Assert.Equal(30, inventory.QuantityReserved);
        Assert.Equal(70, inventory.QuantityAvailable);
        Assert.Same(
            originalMovement,
            Assert.Single(inventory.PendingMovements));
    }

    [Fact]
    public void ReserveStock_WhenQuantityExceedsAvailable_PreservesState()
    {
        var inventory = new Inventory(
            Guid.NewGuid(),
            Guid.NewGuid());

        inventory.ReceiveStock(100, "Initial stock", OccurredAt);
        inventory.ReserveStock(30);

        var originalMovement = Assert.Single(inventory.PendingMovements);

        Assert.Throws<InvalidOperationException>(
            () => inventory.ReserveStock(80));

        Assert.Equal(100, inventory.QuantityOnHand);
        Assert.Equal(30, inventory.QuantityReserved);
        Assert.Equal(70, inventory.QuantityAvailable);
        Assert.Same(
            originalMovement,
            Assert.Single(inventory.PendingMovements));

        inventory.ReserveStock(70);

        Assert.Equal(100, inventory.QuantityOnHand);
        Assert.Equal(100, inventory.QuantityReserved);
        Assert.Equal(0, inventory.QuantityAvailable);
        Assert.Same(
            originalMovement,
            Assert.Single(inventory.PendingMovements));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void ReserveStock_WithNonPositiveQuantity_PreservesState(
    int quantity)
    {
        var inventory = new Inventory(
            Guid.NewGuid(),
            Guid.NewGuid());

        inventory.ReceiveStock(100, "Initial stock", OccurredAt);
        inventory.ReserveStock(30);

        var originalMovement = Assert.Single(inventory.PendingMovements);

        Assert.Throws<ArgumentOutOfRangeException>(
            "quantity",
            () => inventory.ReserveStock(quantity));

        Assert.Equal(100, inventory.QuantityOnHand);
        Assert.Equal(30, inventory.QuantityReserved);
        Assert.Equal(70, inventory.QuantityAvailable);
        Assert.Same(
            originalMovement,
            Assert.Single(inventory.PendingMovements));
    }
}
