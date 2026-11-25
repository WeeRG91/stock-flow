using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Application.Tests.Fakes;
using StockFlow.Application.Transfers.ReceiveTransfer;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Tests;

public sealed class ReceiveTransferCommandHandlerTests
{
    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    [InlineData(false, 5)]
    [InlineData(true, 5)]
    public async Task HandleAsync_ReceivesIntoNewOrExistingDestination(bool existing, int quantity)
    {
        var fixture = new Fixture(existing);
        using var cancellation = new CancellationTokenSource();
        var command = fixture.Command(quantity);

        var result = await fixture.Handler.HandleAsync(command, cancellation.Token);

        Assert.Equal(quantity == 5 ? TransferStatus.Received : TransferStatus.PartiallyReceived,
            fixture.Transfer.Status);
        Assert.Equal(quantity, fixture.Line.ReceivedQuantity);
        Assert.Equal(5 - quantity, fixture.Line.InTransitQuantity);
        Assert.Equal(quantity == 5 ? fixture.Clock.UtcNow : (DateTimeOffset?)null,
            fixture.Transfer.ReceivedAt);
        var balance = Assert.Single(fixture.Receipts.LastSavedInventories);
        if (existing) Assert.Same(fixture.Destination, balance);
        Assert.Equal(fixture.Transfer.DestinationWarehouseId, balance.WarehouseId);
        Assert.Equal(fixture.Line.ProductId, balance.ProductId);
        Assert.Equal((existing ? 7 : 0) + quantity, balance.QuantityOnHand);
        var movement = Assert.Single(balance.PendingMovements);
        Assert.Equal(quantity, movement.QuantityChange);
        Assert.Equal(StockMovementType.Receipt, movement.Type);
        Assert.Equal(balance.Id, movement.InventoryId);
        Assert.Equal(fixture.Clock.UtcNow, movement.OccurredAt);
        Assert.Contains(command.ReceiptId.ToString(), movement.Reason);
        var receipt = Assert.IsType<TransferReceipt>(fixture.Receipts.LastSavedReceipt);
        Assert.Same(receipt, Assert.Single(fixture.Transfer.PendingReceipts));
        Assert.Same(fixture.Transfer, fixture.Receipts.LastSavedTransfer);
        Assert.Equal(command.ReceiptId, result.ReceiptId);
        Assert.Equal(fixture.Transfer.Id, result.TransferId);
        Assert.Equal(fixture.Clock.UtcNow, result.ConfirmedAt);
        Assert.Equal(fixture.User.UserId!.Value, result.ConfirmedByUserId);
        var resultLine = Assert.Single(result.Lines);
        Assert.Equal(fixture.Line.Id, resultLine.TransferLineId);
        Assert.Equal(quantity, resultLine.ReceivedQuantity);
        Assert.Equal(1, fixture.Receipts.SaveCallCount);
        Assert.Equal(cancellation.Token, fixture.Receipts.LookupToken);
        Assert.Equal(cancellation.Token, fixture.Transfers.LookupToken);
        Assert.Equal(cancellation.Token, fixture.Receipts.SaveToken);
        Assert.Equal(fixture.Transfer.Id,
            Assert.Single(fixture.TransferNotifier.Notifications).TransferId);
        Assert.Single(fixture.InventoryNotifier.Notifications);
        Assert.Equal(cancellation.Token, fixture.TransferNotifier.LastToken);
        Assert.Equal(cancellation.Token, fixture.InventoryNotifier.LastCancellationToken);
        Assert.Equal(10, fixture.Source.QuantityOnHand);
        Assert.Empty(fixture.Source.PendingMovements);
        AssertNoSeparateSaves(fixture);
    }

    [Fact]
    public async Task HandleAsync_CompletesPreviouslyPartialTransfer()
    {
        var fixture = new Fixture();
        fixture.Transfer.ApplyReceipt(new TransferReceipt(Guid.NewGuid(), fixture.Transfer.Id,
            fixture.Clock.UtcNow.AddMinutes(-1), Guid.NewGuid(),
            [new TransferReceiptLine(fixture.Line.Id, 2)]));

        await fixture.Handler.HandleAsync(fixture.Command(3));

        Assert.Equal(TransferStatus.Received, fixture.Transfer.Status);
        Assert.Equal(5, fixture.Line.ReceivedQuantity);
        Assert.Equal(0, fixture.Line.InTransitQuantity);
        Assert.Equal(10, fixture.Destination.QuantityOnHand);
        Assert.Equal(3, Assert.Single(fixture.Destination.PendingMovements).QuantityChange);
        Assert.Equal(1, fixture.Receipts.SaveCallCount);
        Assert.Equal(fixture.Transfer.Id,
            Assert.Single(fixture.TransferNotifier.Notifications).TransferId);
        Assert.Single(fixture.InventoryNotifier.Notifications);
    }

    [Fact]
    public async Task HandleAsync_ExactReplayReturnsOriginalReceiptBeforeVersionOrStateChecks()
    {
        var fixture = new Fixture();
        var command = fixture.Command(5);
        var originalActor = Guid.NewGuid();
        var confirmedAt = fixture.Clock.UtcNow.AddMinutes(-1);
        var receipt = new TransferReceipt(command.ReceiptId, fixture.Transfer.Id,
            confirmedAt, originalActor, [new TransferReceiptLine(fixture.Line.Id, 5)]);
        fixture.Transfer.ApplyReceipt(receipt);
        fixture.Receipts.ExistingReceipt = receipt;
        // No transfer lookup is possible: the persisted receipt must answer the retry.
        fixture.Transfers.Loaded = null;

        var result = await fixture.Handler.HandleAsync(
            command with { ExpectedVersion = Guid.NewGuid() });

        Assert.Equal(receipt.Id, result.ReceiptId);
        Assert.Equal(confirmedAt, result.ConfirmedAt);
        Assert.Equal(originalActor, result.ConfirmedByUserId);
        Assert.Equal(5, Assert.Single(result.Lines).ReceivedQuantity);
        Assert.Equal(0, fixture.Transfers.LookupCallCount);
        Assert.Equal(0, fixture.Inventories.GetCallCount);
        Assert.Equal(0, fixture.Receipts.SaveCallCount);
        Assert.Equal(7, fixture.Destination.QuantityOnHand);
        Assert.Empty(fixture.Destination.PendingMovements);
        AssertNoNotifications(fixture);
    }

    [Fact]
    public async Task HandleAsync_ReplayIgnoresLineOrdering()
    {
        var fixture = new Fixture();
        var command = fixture.Command();
        var secondLineId = Guid.NewGuid();
        fixture.Receipts.ExistingReceipt = new TransferReceipt(
            command.ReceiptId, fixture.Transfer.Id, fixture.Clock.UtcNow, Guid.NewGuid(),
            [new TransferReceiptLine(fixture.Line.Id, 2), new TransferReceiptLine(secondLineId, 1)]);

        var result = await fixture.Handler.HandleAsync(command with
        {
            Lines = [new(secondLineId, 1), new(fixture.Line.Id, 2)]
        });

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(0, fixture.Transfers.LookupCallCount);
        Assert.Equal(0, fixture.Receipts.SaveCallCount);
        AssertNoNotifications(fixture);
    }

    [Theory]
    [InlineData("transfer")]
    [InlineData("quantity")]
    [InlineData("line")]
    [InlineData("count")]
    public async Task HandleAsync_ReceiptIdReusedWithDifferentContents_Conflicts(string difference)
    {
        var fixture = new Fixture();
        var command = fixture.Command();
        fixture.Receipts.ExistingReceipt = new TransferReceipt(command.ReceiptId,
            difference == "transfer" ? Guid.NewGuid() : fixture.Transfer.Id,
            fixture.Clock.UtcNow, Guid.NewGuid(),
            difference == "count"
                ? [new(fixture.Line.Id, 2), new(Guid.NewGuid(), 1)]
                : [new(difference == "line" ? Guid.NewGuid() : fixture.Line.Id,
                    difference == "quantity" ? 3 : 2)]);

        await Assert.ThrowsAsync<TransferConflictException>(
            () => fixture.Handler.HandleAsync(command));

        Assert.Equal(0, fixture.Transfers.LookupCallCount);
        AssertUnchanged(fixture);
    }

    [Theory]
    [InlineData("receipt-id")]
    [InlineData("transfer-id")]
    [InlineData("version")]
    [InlineData("null-lines")]
    [InlineData("empty-lines")]
    [InlineData("null-line")]
    [InlineData("line-id")]
    [InlineData("duplicate")]
    [InlineData("zero")]
    [InlineData("negative")]
    public async Task HandleAsync_InvalidCommand_RejectsBeforeLookup(string invalid)
    {
        var fixture = new Fixture();
        var command = fixture.Command();
        command = invalid switch
        {
            "receipt-id" => command with { ReceiptId = Guid.Empty },
            "transfer-id" => command with { TransferId = Guid.Empty },
            "version" => command with { ExpectedVersion = Guid.Empty },
            "null-lines" => command with { Lines = null! },
            "empty-lines" => command with { Lines = [] },
            "null-line" => command with { Lines = [null!] },
            "line-id" => command with { Lines = [new(Guid.Empty, 2)] },
            "duplicate" => command with { Lines = [new(fixture.Line.Id, 1), new(fixture.Line.Id, 1)] },
            "zero" => command with { Lines = [new(fixture.Line.Id, 0)] },
            _ => command with { Lines = [new(fixture.Line.Id, -1)] }
        };

        await Assert.ThrowsAsync<RequestValidationException>(
            () => fixture.Handler.HandleAsync(command));

        Assert.Equal(0, fixture.Receipts.LookupCallCount);
        Assert.Equal(0, fixture.Transfers.LookupCallCount);
        AssertUnchanged(fixture);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("empty-user")]
    [InlineData("dispatch-only")]
    public async Task HandleAsync_WithoutReceivingPermission_RejectsBeforeReplay(string scenario)
    {
        var fixture = new Fixture();
        fixture.User.Permissions.Clear();
        fixture.User.Permissions.Add(StockFlowPolicies.TransfersDispatch);
        if (scenario == "anonymous") fixture.User.UserId = null;
        if (scenario == "empty-user") fixture.User.UserId = Guid.Empty;
        var command = fixture.Command();
        fixture.Receipts.ExistingReceipt = new TransferReceipt(command.ReceiptId,
            fixture.Transfer.Id, fixture.Clock.UtcNow, Guid.NewGuid(),
            [new(fixture.Line.Id, 2)]);

        if (scenario == "dispatch-only")
            await Assert.ThrowsAsync<PermissionDeniedException>(
                () => fixture.Handler.HandleAsync(command));
        else
            await Assert.ThrowsAsync<AuthenticationRequiredException>(
                () => fixture.Handler.HandleAsync(command));

        Assert.Equal(0, fixture.Receipts.LookupCallCount);
        AssertUnchanged(fixture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_MissingOrStaleTransfer_DoesNotLoadInventory(bool stale)
    {
        var fixture = new Fixture();
        if (!stale) fixture.Transfers.Loaded = null;
        var command = fixture.Command() with { ExpectedVersion = Guid.NewGuid() };
        if (stale)
            await Assert.ThrowsAsync<TransferConflictException>(() => fixture.Handler.HandleAsync(command));
        else
            await Assert.ThrowsAsync<ResourceNotFoundException>(() => fixture.Handler.HandleAsync(command));
        Assert.Equal(0, fixture.Inventories.GetCallCount);
        AssertUnchanged(fixture);
    }

    [Theory]
    [InlineData(TransferStatus.Draft)]
    [InlineData(TransferStatus.Cancelled)]
    [InlineData(TransferStatus.Received)]
    public async Task HandleAsync_NonReceivableStatus_DoesNotSave(TransferStatus status)
    {
        var fixture = new Fixture(dispatch: false);
        if (status == TransferStatus.Cancelled)
            fixture.Transfer.Cancel(fixture.Clock.UtcNow, fixture.User.UserId!.Value);
        if (status == TransferStatus.Received)
        {
            fixture.Transfer.Dispatch(fixture.Clock.UtcNow.AddMinutes(-2), fixture.User.UserId!.Value);
            fixture.Transfer.ApplyReceipt(new TransferReceipt(Guid.NewGuid(), fixture.Transfer.Id,
                fixture.Clock.UtcNow.AddMinutes(-1), fixture.User.UserId.Value,
                [new(fixture.Line.Id, 5)]));
        }

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => fixture.Handler.HandleAsync(fixture.Command()));

        Assert.Equal(status, fixture.Transfer.Status);
        Assert.Equal(0, fixture.Receipts.SaveCallCount);
        Assert.Empty(fixture.Destination.PendingMovements);
        Assert.Equal(7, fixture.Destination.QuantityOnHand);
        AssertNoNotifications(fixture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_ForeignLineOrExcessQuantity_PreservesState(bool excessive)
    {
        var fixture = new Fixture();
        var command = fixture.Command() with
        {
            Lines = [new(excessive ? fixture.Line.Id : Guid.NewGuid(), excessive ? 6 : 2)]
        };
        if (excessive)
            await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.Handler.HandleAsync(command));
        else
            await Assert.ThrowsAsync<RequestValidationException>(() => fixture.Handler.HandleAsync(command));
        Assert.Equal(0, fixture.Inventories.GetCallCount);
        AssertUnchanged(fixture);
    }

    [Fact]
    public async Task HandleAsync_LaterBalanceWouldOverflow_PreservesEveryLineAndBalance()
    {
        var fixture = new Fixture(dispatch: false);
        var productId = Guid.NewGuid();
        fixture.Transfer.AddLine(productId, "DESK", "Desk", 3);
        fixture.Transfer.Dispatch(fixture.Clock.UtcNow.AddMinutes(-2), fixture.User.UserId!.Value);
        var second = fixture.Transfer.Lines[1];
        var fullBalance = Fixture.Stock(productId, fixture.Transfer.DestinationWarehouseId, int.MaxValue);
        fixture.Inventories.Add(fullBalance);
        var command = fixture.Command() with
        {
            Lines = [new(fixture.Line.Id, 2), new(second.Id, 1)]
        };

        await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.Handler.HandleAsync(command));

        AssertUnchanged(fixture);
        Assert.All(fixture.Transfer.Lines, line => Assert.Equal(0, line.ReceivedQuantity));
        Assert.Equal(int.MaxValue, fullBalance.QuantityOnHand);
        Assert.Empty(fullBalance.PendingMovements);
    }

    [Fact]
    public async Task HandleAsync_CancelledToken_DoesNotLookup()
    {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Handler.HandleAsync(fixture.Command(), cancellation.Token));
        Assert.Equal(0, fixture.Receipts.LookupCallCount);
        AssertUnchanged(fixture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_SaveFailure_PropagatesWithoutRetryOrAcknowledgment(bool conflict)
    {
        var fixture = new Fixture();
        Exception failure = conflict
            ? new TransferConflictException(fixture.Transfer.Id, "Concurrent receipt.")
            : new InvalidOperationException("Persistence failed.");
        fixture.Receipts.SaveFailure = failure;

        var error = await Record.ExceptionAsync(() => fixture.Handler.HandleAsync(fixture.Command()));

        Assert.Same(failure, error);
        Assert.Equal(1, fixture.Receipts.SaveCallCount);
        Assert.Null(fixture.Receipts.LastSavedReceipt);
        Assert.Single(fixture.Transfer.PendingReceipts);
        Assert.Single(fixture.Destination.PendingMovements);
        // In-memory changes remain; discard this scope after the failed save.
        Assert.Equal(TransferStatus.PartiallyReceived, fixture.Transfer.Status);
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
        Assert.Equal(0, fixture.Transfers.DispatchSaveCallCount);
        Assert.Equal(0, fixture.Transfers.CancellationSaveCallCount);
    }

    private static void AssertUnchanged(Fixture fixture)
    {
        Assert.Equal(TransferStatus.Dispatched, fixture.Transfer.Status);
        Assert.Null(fixture.Transfer.ReceivedAt);
        Assert.Equal(0, fixture.Line.ReceivedQuantity);
        Assert.Empty(fixture.Transfer.PendingReceipts);
        Assert.Equal(7, fixture.Destination.QuantityOnHand);
        Assert.Empty(fixture.Destination.PendingMovements);
        Assert.Equal(10, fixture.Source.QuantityOnHand);
        Assert.Empty(fixture.Source.PendingMovements);
        Assert.Equal(0, fixture.Receipts.SaveCallCount);
        AssertNoNotifications(fixture);
        AssertNoSeparateSaves(fixture);
    }

    private sealed class Fixture
    {
        public FakeCurrentUser User { get; } = new() { UserId = Guid.NewGuid() };
        public FakeTransferReceiptRepository Receipts { get; } = new();
        public FakeTransferRepository Transfers { get; } = new();
        public FakeInventoryRepository Inventories { get; } = new();
        public FakeTransferChangeNotifier TransferNotifier { get; } = new();
        public FakeInventoryChangeNotifier InventoryNotifier { get; } = new();
        public FakeClock Clock { get; } = new()
        {
            UtcNow = new DateTimeOffset(2026, 11, 23, 11, 0, 0, TimeSpan.Zero)
        };
        public Transfer Transfer { get; }
        public TransferLine Line => Transfer.Lines[0];
        public InventoryEntity Source { get; }
        public InventoryEntity Destination { get; }
        public Guid Version { get; } = Guid.NewGuid();
        public ReceiveTransferCommandHandler Handler { get; }

        public Fixture(bool existing = true, bool dispatch = true)
        {
            User.Permissions.Add(StockFlowPolicies.TransfersReceive);
            Transfer = new Transfer(Guid.NewGuid(), "BRU", "Brussels",
                Guid.NewGuid(), "LIE", "Liège", Clock.UtcNow.AddHours(-1), User.UserId!.Value);
            Transfer.AddLine(Guid.NewGuid(), "CHAIR", "Chair", 5);
            if (dispatch) Transfer.Dispatch(Clock.UtcNow.AddMinutes(-2), User.UserId.Value);
            Source = Stock(Line.ProductId, Transfer.SourceWarehouseId, 10);
            Destination = Stock(Line.ProductId, Transfer.DestinationWarehouseId, 7);
            Inventories.Add(Source);
            if (existing) Inventories.Add(Destination);
            Transfers.Loaded = (Transfer, Version);
            TransferNotifier.OnNotify = notification =>
            {
                AssertReceiptSaved();
                Assert.Equal(Transfer.Id, notification.TransferId);
            };
            InventoryNotifier.OnNotify = _ => AssertReceiptSaved();
            Handler = new ReceiveTransferCommandHandler(
                User, Receipts, Transfers, Inventories, Clock,
                TransferNotifier, InventoryNotifier);
        }

        private void AssertReceiptSaved()
        {
            Assert.Equal(1, Receipts.SaveCallCount);
            Assert.Same(Transfer, Receipts.LastSavedTransfer);
            var receipt = Assert.IsType<TransferReceipt>(Receipts.LastSavedReceipt);
            Assert.Equal(Transfer.Id, receipt.TransferId);
            Assert.Contains(receipt, Transfer.PendingReceipts);
            Assert.NotEmpty(Receipts.LastSavedInventories);
        }

        public ReceiveTransferCommand Command(int quantity = 2) =>
            new(Guid.NewGuid(), Transfer.Id, Version, [new(Line.Id, quantity)]);

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
