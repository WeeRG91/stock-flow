using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Application.Tests.Fakes;
using StockFlow.Application.Transfers.DispatchTransfer;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Tests;

public sealed class DispatchTransferCommandHandlerTests
{
    [Theory]
    [InlineData(7)]
    [InlineData(10)]
    public async Task HandleAsync_WhenSourceStockIsReserved_RejectsBeforeMutation(int reserved)
    {
        var fixture = new Fixture();
        fixture.SourceInventory.ReserveStock(reserved);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => fixture.Handler.HandleAsync(fixture.Command));

        AssertUnchanged(fixture);
        Assert.Equal(reserved, fixture.SourceInventory.QuantityReserved);
        Assert.Equal(10 - reserved, fixture.SourceInventory.QuantityAvailable);
        Assert.All(fixture.Transfer.Lines, line => Assert.Equal(0, line.DispatchedQuantity));
    }

    [Fact]
    public async Task HandleAsync_WhenDispatchUsesExactlyAvailableStock_PreservesReservations()
    {
        var fixture = new Fixture(requested: 4);
        fixture.SourceInventory.ReserveStock(6);

        await fixture.Handler.HandleAsync(fixture.Command);

        Assert.Equal(TransferStatus.Dispatched, fixture.Transfer.Status);
        Assert.Equal(6, fixture.SourceInventory.QuantityOnHand);
        Assert.Equal(6, fixture.SourceInventory.QuantityReserved);
        Assert.Equal(0, fixture.SourceInventory.QuantityAvailable);
        Assert.Equal(-4, Assert.Single(fixture.SourceInventory.PendingMovements).QuantityChange);
        Assert.Equal(7, fixture.DestinationInventory.QuantityOnHand);
        Assert.Equal(1, fixture.Transfers.DispatchSaveCallCount);
        Assert.Single(fixture.TransferNotifier.Notifications);
        Assert.Single(fixture.InventoryNotifier.Notifications);
        AssertNoSeparateSaves(fixture);
    }

    [Fact]
    public async Task HandleAsync_WhenLaterLineHasReservedStock_PreservesEarlierLinesAndInventory()
    {
        var fixture = new Fixture();
        var product = new Product("Synthetic desk", "TEST-DESK");
        fixture.Products.Add(product);
        fixture.Transfer.AddLine(product.Id, product.Sku, product.Name, 3);
        var inventory = Fixture.Stock(product.Id, fixture.Source.Id, 5);
        inventory.ReserveStock(3);
        fixture.Inventories.Add(inventory);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => fixture.Handler.HandleAsync(fixture.Command));

        AssertUnchanged(fixture);
        Assert.Equal(0, fixture.SourceInventory.QuantityReserved);
        Assert.Equal(5, inventory.QuantityOnHand);
        Assert.Equal(3, inventory.QuantityReserved);
        Assert.Equal(2, inventory.QuantityAvailable);
        Assert.Empty(inventory.PendingMovements);
        Assert.All(fixture.Transfer.Lines, line => Assert.Equal(0, line.DispatchedQuantity));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(10)]
    public async Task HandleAsync_DispatchesAndRemovesSourceStockInOneSave(int requested)
    {
        var fixture = new Fixture(requested: requested);
        using var cancellation = new CancellationTokenSource();

        await fixture.Handler.HandleAsync(fixture.Command, cancellation.Token);

        Assert.Equal(TransferStatus.Dispatched, fixture.Transfer.Status);
        Assert.Equal(fixture.Clock.UtcNow, fixture.Transfer.DispatchedAt);
        Assert.Equal(fixture.User.UserId, fixture.Transfer.DispatchedByUserId);
        Assert.Equal(requested, Assert.Single(fixture.Transfer.Lines).InTransitQuantity);
        Assert.Equal(10 - requested, fixture.SourceInventory.QuantityOnHand);
        var movement = Assert.Single(fixture.SourceInventory.PendingMovements);
        Assert.Equal(-requested, movement.QuantityChange);
        Assert.Equal(StockMovementType.Removal, movement.Type);
        Assert.Equal(fixture.SourceInventory.Id, movement.InventoryId);
        Assert.Equal(fixture.Clock.UtcNow, movement.OccurredAt);
        Assert.Contains(fixture.Transfer.Id.ToString(), movement.Reason);
        Assert.Equal(7, fixture.DestinationInventory.QuantityOnHand);
        Assert.Empty(fixture.DestinationInventory.PendingMovements);
        Assert.Equal(1, fixture.Transfers.DispatchSaveCallCount);
        Assert.Same(fixture.Transfer, fixture.Transfers.LastSavedTransfer);
        Assert.Same(fixture.SourceInventory,
            Assert.Single(fixture.Transfers.LastDispatchInventories));
        Assert.Equal(cancellation.Token, fixture.Transfers.LookupToken);
        Assert.Equal(cancellation.Token, fixture.Transfers.SaveToken);
        Assert.Equal(fixture.Transfer.Id,
            Assert.Single(fixture.TransferNotifier.Notifications).TransferId);
        Assert.Single(fixture.InventoryNotifier.Notifications);
        Assert.Equal(cancellation.Token, fixture.TransferNotifier.LastToken);
        Assert.Equal(cancellation.Token, fixture.InventoryNotifier.LastCancellationToken);
        AssertNoSeparateSaves(fixture);
    }

    [Fact]
    public async Task HandleAsync_WhenLaterLineLacksStock_PreservesAllState()
    {
        var fixture = new Fixture();
        var product = new Product("Desk", "DESK");
        fixture.Products.Add(product);
        fixture.Transfer.AddLine(product.Id, product.Sku, product.Name, 3);
        var inventory = Fixture.Stock(product.Id, fixture.Source.Id, 2);
        fixture.Inventories.Add(inventory);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => fixture.Handler.HandleAsync(fixture.Command));

        AssertUnchanged(fixture);
        Assert.Equal(2, inventory.QuantityOnHand);
        Assert.Empty(inventory.PendingMovements);
        Assert.All(fixture.Transfer.Lines, line => Assert.Equal(0, line.DispatchedQuantity));
    }

    [Theory]
    [InlineData("source-missing")]
    [InlineData("destination-missing")]
    [InlineData("source-inactive")]
    [InlineData("destination-inactive")]
    [InlineData("product-missing")]
    [InlineData("product-inactive")]
    [InlineData("inventory-missing")]
    [InlineData("insufficient")]
    public async Task HandleAsync_WithUnavailableResources_DoesNotMutate(string scenario)
    {
        var fixture = new Fixture(scenario);

        if (scenario is "source-missing" or "destination-missing" or "product-missing")
            await Assert.ThrowsAsync<ResourceNotFoundException>(
                () => fixture.Handler.HandleAsync(fixture.Command));
        else
            await Assert.ThrowsAsync<BusinessRuleException>(
                () => fixture.Handler.HandleAsync(fixture.Command));

        AssertUnchanged(fixture);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("empty-user")]
    [InlineData("manage-only")]
    public async Task HandleAsync_WithoutDispatchPermission_DoesNotLookup(string scenario)
    {
        var fixture = new Fixture();
        fixture.User.Permissions.Clear();
        fixture.User.Permissions.Add(StockFlowPolicies.TransfersManage);
        if (scenario == "anonymous") fixture.User.UserId = null;
        if (scenario == "empty-user") fixture.User.UserId = Guid.Empty;

        if (scenario == "manage-only")
            await Assert.ThrowsAsync<PermissionDeniedException>(
                () => fixture.Handler.HandleAsync(fixture.Command));
        else
            await Assert.ThrowsAsync<AuthenticationRequiredException>(
                () => fixture.Handler.HandleAsync(fixture.Command));

        Assert.Equal(0, fixture.Transfers.LookupCallCount);
        AssertUnchanged(fixture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_WithEmptyIdentifier_DoesNotLookup(bool emptyVersion)
    {
        var fixture = new Fixture();
        var command = emptyVersion
            ? fixture.Command with { ExpectedVersion = Guid.Empty }
            : fixture.Command with { TransferId = Guid.Empty };

        await Assert.ThrowsAsync<RequestValidationException>(
            () => fixture.Handler.HandleAsync(command));
        Assert.Equal(0, fixture.Transfers.LookupCallCount);
        AssertUnchanged(fixture);
    }

    [Fact]
    public async Task HandleAsync_WithMissingTransfer_DoesNotMutate()
    {
        var fixture = new Fixture();
        fixture.Transfers.Loaded = null;
        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => fixture.Handler.HandleAsync(fixture.Command));
        AssertUnchanged(fixture);
    }

    [Fact]
    public async Task HandleAsync_WithStaleVersion_DoesNotLoadStock()
    {
        var fixture = new Fixture();
        var command = fixture.Command with { ExpectedVersion = Guid.NewGuid() };
        await Assert.ThrowsAsync<TransferConflictException>(
            () => fixture.Handler.HandleAsync(command));
        Assert.Equal(0, fixture.Inventories.GetCallCount);
        AssertUnchanged(fixture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_WithNonDraft_DoesNotDispatchAgain(bool cancelled)
    {
        var fixture = new Fixture();
        if (cancelled)
            fixture.Transfer.Cancel(fixture.Clock.UtcNow, fixture.User.UserId!.Value);
        else
            fixture.Transfer.Dispatch(fixture.Clock.UtcNow, fixture.User.UserId!.Value);
        var status = fixture.Transfer.Status;

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => fixture.Handler.HandleAsync(fixture.Command));

        Assert.Equal(status, fixture.Transfer.Status);
        Assert.Equal(10, fixture.SourceInventory.QuantityOnHand);
        Assert.Empty(fixture.SourceInventory.PendingMovements);
        Assert.Equal(0, fixture.Transfers.DispatchSaveCallCount);
        AssertNoNotifications(fixture);
        AssertNoSeparateSaves(fixture);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyDraft_DoesNotSave()
    {
        var fixture = new Fixture();
        fixture.Transfer.ReplaceDraftLines([]);
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => fixture.Handler.HandleAsync(fixture.Command));
        AssertUnchanged(fixture);
    }

    [Fact]
    public async Task HandleAsync_WithCancelledToken_DoesNotLookup()
    {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Handler.HandleAsync(fixture.Command, cancellation.Token));
        Assert.Equal(0, fixture.Transfers.LookupCallCount);
        AssertUnchanged(fixture);
    }

    [Fact]
    public async Task HandleAsync_WhenSaveConflicts_PropagatesWithoutRetryOrAcknowledgment()
    {
        var fixture = new Fixture();
        var failure = new TransferConflictException(fixture.Transfer.Id, "Simulated conflict.");
        fixture.Transfers.SaveFailure = failure;

        var error = await Assert.ThrowsAsync<TransferConflictException>(
            () => fixture.Handler.HandleAsync(fixture.Command));

        Assert.Same(failure, error);
        Assert.Equal(1, fixture.Transfers.DispatchSaveCallCount);
        Assert.Null(fixture.Transfers.LastSavedTransfer);
        Assert.Single(fixture.SourceInventory.PendingMovements);
        // Mutated in-memory objects must be discarded after a failed save.
        Assert.Equal(TransferStatus.Dispatched, fixture.Transfer.Status);
        AssertNoNotifications(fixture);
        AssertNoSeparateSaves(fixture);
    }

    private static void AssertNoNotifications(Fixture fixture)
    {
        Assert.Empty(fixture.TransferNotifier.Notifications);
        Assert.Empty(fixture.InventoryNotifier.Notifications);
    }

    private static void AssertNoSeparateSaves(Fixture fixture)
    {
        Assert.Equal(0, fixture.Inventories.SaveCallCount);
        Assert.Equal(0, fixture.Transfers.SaveCallCount);
        Assert.Equal(0, fixture.Transfers.CancellationSaveCallCount);
    }

    private static void AssertUnchanged(Fixture fixture)
    {
        Assert.Equal(TransferStatus.Draft, fixture.Transfer.Status);
        Assert.Null(fixture.Transfer.DispatchedAt);
        Assert.Null(fixture.Transfer.DispatchedByUserId);
        Assert.Equal(10, fixture.SourceInventory.QuantityOnHand);
        Assert.Empty(fixture.SourceInventory.PendingMovements);
        Assert.Equal(7, fixture.DestinationInventory.QuantityOnHand);
        Assert.Empty(fixture.DestinationInventory.PendingMovements);
        Assert.Equal(0, fixture.Transfers.DispatchSaveCallCount);
        AssertNoNotifications(fixture);
        AssertNoSeparateSaves(fixture);
    }

    private sealed class Fixture
    {
        public FakeCurrentUser User { get; } = new() { UserId = Guid.NewGuid() };
        public FakeTransferRepository Transfers { get; } = new();
        public FakeInventoryRepository Inventories { get; } = new();
        public FakeWarehouseRepository Warehouses { get; } = new();
        public FakeProductRepository Products { get; } = new();
        public FakeTransferChangeNotifier TransferNotifier { get; } = new();
        public FakeInventoryChangeNotifier InventoryNotifier { get; } = new();
        public FakeClock Clock { get; } = new()
        {
            UtcNow = new DateTimeOffset(2026, 11, 23, 11, 0, 0, TimeSpan.Zero)
        };
        public Warehouse Source { get; } = new("BRU", "Brussels", "Belgium");
        public Warehouse Destination { get; } = new("LIE", "Liège", "Belgium");
        public InventoryEntity SourceInventory { get; }
        public InventoryEntity DestinationInventory { get; }
        public Transfer Transfer { get; }
        public DispatchTransferCommand Command { get; }
        public DispatchTransferCommandHandler Handler { get; }

        public Fixture(string scenario = "ready", int requested = 4)
        {
            User.Permissions.Add(StockFlowPolicies.TransfersDispatch);
            if (scenario == "source-inactive") Source.Deactivate();
            if (scenario == "destination-inactive") Destination.Deactivate();
            if (scenario != "source-missing") Warehouses.Add(Source);
            if (scenario != "destination-missing") Warehouses.Add(Destination);

            var product = new Product("Chair", "CHAIR");
            if (scenario == "product-inactive") product.Deactivate();
            if (scenario != "product-missing") Products.Add(product);
            Transfer = new Transfer(Source.Id, Source.Code, Source.Name,
                Destination.Id, Destination.Code, Destination.Name,
                Clock.UtcNow.AddHours(-1), User.UserId!.Value);
            Transfer.AddLine(product.Id, product.Sku, product.Name,
                scenario == "insufficient" ? 11 : requested);
            SourceInventory = Stock(product.Id, Source.Id, 10);
            DestinationInventory = Stock(product.Id, Destination.Id, 7);
            if (scenario != "inventory-missing") Inventories.Add(SourceInventory);
            Inventories.Add(DestinationInventory);
            var version = Guid.NewGuid();
            Transfers.Loaded = (Transfer, version);
            Command = new DispatchTransferCommand(Transfer.Id, version);
            TransferNotifier.OnNotify = notification =>
            {
                AssertDispatchSaved();
                Assert.Equal(Transfer.Id, notification.TransferId);
            };
            InventoryNotifier.OnNotify = _ => AssertDispatchSaved();
            Handler = new DispatchTransferCommandHandler(
                Transfers, Inventories, Warehouses, Products, User, Clock,
                TransferNotifier, InventoryNotifier);
        }

        private void AssertDispatchSaved()
        {
            Assert.Equal(1, Transfers.DispatchSaveCallCount);
            Assert.Same(Transfer, Transfers.LastSavedTransfer);
            Assert.Same(SourceInventory, Assert.Single(Transfers.LastDispatchInventories));
            Assert.Equal(TransferStatus.Dispatched, Transfer.Status);
        }

        public static InventoryEntity Stock(Guid productId, Guid warehouseId, int quantity)
        {
            var inventory = new InventoryEntity(productId, warehouseId);
            inventory.ReceiveStock(quantity, "Opening stock",
                new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero));
            inventory.AcknowledgePersistedMovements(
                inventory.PendingMovements.Select(movement => movement.Id).ToArray());
            return inventory;
        }
    }
}
