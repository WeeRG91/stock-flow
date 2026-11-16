using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Application.GoodsReceipts.ConfirmGoodsReceipt;
using StockFlow.Application.Tests.Fakes;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Tests;

public class ConfirmGoodsReceiptCommandHandlerTests
{
    [Theory]
    [InlineData(4, false, PurchaseOrderStatus.PartiallyReceived)]
    [InlineData(10, true, PurchaseOrderStatus.Received)]
    public async Task HandleAsync_WithValidDelivery_SavesAllChangesTogether(
        int quantity, bool hasExistingBalance, PurchaseOrderStatus expectedStatus)
    {
        var fixture = new Fixture();
        var line = Assert.Single(fixture.Order.Lines);
        InventoryEntity? existingBalance = null;

        if (hasExistingBalance)
        {
            existingBalance = new InventoryEntity(line.ProductId, fixture.Order.WarehouseId);
            existingBalance.ReceiveStock(3, "Opening balance", fixture.Clock.UtcNow);
            existingBalance.AcknowledgePersistedMovements(
                existingBalance.PendingMovements.Select(movement => movement.Id).ToArray());
            fixture.Inventories.Add(existingBalance);
        }

        var command = fixture.Command(quantity);
        var result = await fixture.Handler.HandleAsync(command);

        Assert.Equal(command.ReceiptId, result.ReceiptId);
        Assert.Equal(fixture.Clock.UtcNow, result.ConfirmedAt);
        Assert.Equal(fixture.User.UserId, result.ConfirmedByUserId);
        Assert.Equal(
            new ConfirmedGoodsReceiptLineDto(line.Id, quantity),
            Assert.Single(result.Lines));

        Assert.Equal(expectedStatus, fixture.Order.Status);
        Assert.Equal(quantity, line.ReceivedQuantity);
        Assert.Equal(1, fixture.Receipts.SaveCallCount);
        Assert.Equal(command.ReceiptId, fixture.Receipts.LastSavedReceipt?.Id);
        Assert.Same(fixture.Order, fixture.Receipts.LastSavedOrder);
        Assert.Equal(0, fixture.Orders.SaveCallCount);
        Assert.Equal(0, fixture.Inventories.SaveCallCount);

        var balance = Assert.Single(fixture.Receipts.LastSavedInventories);
        Assert.Equal(line.ProductId, balance.ProductId);
        Assert.Equal(fixture.Order.WarehouseId, balance.WarehouseId);
        Assert.Equal(quantity + (hasExistingBalance ? 3 : 0), balance.QuantityOnHand);
        if (existingBalance is not null)
        {
            Assert.Same(existingBalance, balance);
        }

        var movement = Assert.Single(balance.PendingMovements);
        Assert.Equal(quantity, movement.QuantityChange);
        Assert.Equal(StockMovementType.Receipt, movement.Type);
    }

    [Fact]
    public async Task HandleAsync_WithExactReplay_ReturnsOriginalReceiptWithoutWriting()
    {
        var fixture = new Fixture();
        var command = fixture.Command(4) with { ExpectedVersion = Guid.NewGuid() };
        var originalTime = fixture.Clock.UtcNow.AddMinutes(-1);
        var originalUser = Guid.NewGuid();
        fixture.Receipts.ExistingReceipt = new GoodsReceipt(
            command.ReceiptId, command.PurchaseOrderId, originalTime, originalUser,
            new[] { new GoodsReceiptLine(command.Lines[0].PurchaseOrderLineId, 4) });

        var result = await fixture.Handler.HandleAsync(command);

        Assert.Equal(command.ReceiptId, result.ReceiptId);
        Assert.Equal(originalTime, result.ConfirmedAt);
        Assert.Equal(originalUser, result.ConfirmedByUserId);
        Assert.Equal(4, Assert.Single(result.Lines).ReceivedQuantity);
        Assert.Equal(0, fixture.Orders.LookupCallCount);
        Assert.Equal(0, fixture.Inventories.GetCallCount);
        Assert.Equal(0, fixture.Receipts.SaveCallCount);
    }

    [Fact]
    public async Task HandleAsync_WithReusedReceiptIdAndDifferentQuantity_ReturnsConflict()
    {
        var fixture = new Fixture();
        var command = fixture.Command(4);
        fixture.Receipts.ExistingReceipt = new GoodsReceipt(
            command.ReceiptId, command.PurchaseOrderId, fixture.Clock.UtcNow, Guid.NewGuid(),
            new[] { new GoodsReceiptLine(command.Lines[0].PurchaseOrderLineId, 3) });

        await Assert.ThrowsAsync<PurchaseOrderConflictException>(
            () => fixture.Handler.HandleAsync(command));

        Assert.Equal(0, fixture.Orders.LookupCallCount);
        Assert.Equal(0, fixture.Inventories.GetCallCount);
        Assert.Equal(0, fixture.Receipts.SaveCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_WithoutReceivingAccess_RejectsBeforeReceiptLookup(
        bool isAuthenticated)
    {
        var fixture = new Fixture();
        fixture.User.Permissions.Clear();
        if (!isAuthenticated)
        {
            fixture.User.UserId = null;
        }

        var command = fixture.Command(4);
        if (isAuthenticated)
        {
            await Assert.ThrowsAsync<PermissionDeniedException>(
                () => fixture.Handler.HandleAsync(command));
        }
        else
        {
            await Assert.ThrowsAsync<AuthenticationRequiredException>(
                () => fixture.Handler.HandleAsync(command));
        }

        Assert.Equal(0, fixture.Receipts.LookupCallCount);
        Assert.Equal(0, fixture.Receipts.SaveCallCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task HandleAsync_WithInvalidQuantity_ReportsTheLineField(int quantity)
    {
        var fixture = new Fixture();

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => fixture.Handler.HandleAsync(fixture.Command(quantity)));

        Assert.Equal("Lines[0].ReceivedQuantity", exception.FieldName);
        Assert.Equal(0, fixture.Receipts.LookupCallCount);
        Assert.Equal(0, fixture.Receipts.SaveCallCount);
    }

    [Fact]
    public async Task HandleAsync_WithStaleVersion_RejectsBeforeInventoryLookup()
    {
        var fixture = new Fixture();
        var command = fixture.Command(4) with { ExpectedVersion = Guid.NewGuid() };

        await Assert.ThrowsAsync<PurchaseOrderConflictException>(
            () => fixture.Handler.HandleAsync(command));

        Assert.Equal(PurchaseOrderStatus.Approved, fixture.Order.Status);
        Assert.Equal(0, Assert.Single(fixture.Order.Lines).ReceivedQuantity);
        Assert.Equal(0, fixture.Inventories.GetCallCount);
        Assert.Equal(0, fixture.Receipts.SaveCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenDeliveryExceedsOrderedQuantity_DoesNotSave()
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => fixture.Handler.HandleAsync(fixture.Command(11)));

        Assert.Equal(PurchaseOrderStatus.Approved, fixture.Order.Status);
        Assert.Equal(0, Assert.Single(fixture.Order.Lines).ReceivedQuantity);
        Assert.Equal(0, fixture.Inventories.GetCallCount);
        Assert.Equal(0, fixture.Receipts.SaveCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenStockWouldOverflow_DoesNotSaveOrIncreaseStock()
    {
        var fixture = new Fixture();
        var line = Assert.Single(fixture.Order.Lines);
        var balance = new InventoryEntity(line.ProductId, fixture.Order.WarehouseId);
        balance.ReceiveStock(int.MaxValue, "Opening balance", fixture.Clock.UtcNow);
        fixture.Inventories.Add(balance);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => fixture.Handler.HandleAsync(fixture.Command(1)));

        Assert.Equal(int.MaxValue, balance.QuantityOnHand);
        Assert.Single(balance.PendingMovements);
        Assert.Equal(0, fixture.Receipts.SaveCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenCombinedSaveReportsConflict_DoesNotRetry()
    {
        var fixture = new Fixture();
        var conflict = new PurchaseOrderConflictException(
            fixture.Order.Id, "The purchase order has changed.");
        fixture.Receipts.SaveFailure = conflict;

        var exception = await Assert.ThrowsAsync<PurchaseOrderConflictException>(
            () => fixture.Handler.HandleAsync(fixture.Command(4)));

        Assert.Same(conflict, exception);
        Assert.Equal(1, fixture.Receipts.SaveCallCount);
        Assert.Null(fixture.Receipts.LastSavedReceipt);
        Assert.Equal(0, fixture.Orders.SaveCallCount);
        Assert.Equal(0, fixture.Inventories.SaveCallCount);
    }

    private sealed class Fixture
    {
        private readonly Guid _version = Guid.NewGuid();

        public FakeCurrentUser User { get; } = new() { UserId = Guid.NewGuid() };
        public FakeClock Clock { get; } = new();
        public FakeGoodsReceiptRepository Receipts { get; } = new();
        public FakePurchaseOrderRepository Orders { get; } = new();
        public FakeInventoryRepository Inventories { get; } = new();
        public PurchaseOrder Order { get; }
        public ConfirmGoodsReceiptCommandHandler Handler { get; }

        public Fixture()
        {
            User.Permissions.Add(StockFlowPolicies.InventoryReceive);
            Order = new PurchaseOrder(
                "PO-TEST", Guid.NewGuid(), "SUP-01", "Demo supplier",
                Guid.NewGuid(), "BRU", "Brussels", Clock.UtcNow.AddMinutes(-3),
                Guid.NewGuid());
            Order.AddLine(Guid.NewGuid(), "CHAIR", "Office chair", 10, 25m);
            Order.Submit(Clock.UtcNow.AddMinutes(-2));
            Order.Approve(Clock.UtcNow.AddMinutes(-1), Guid.NewGuid());
            Orders.Loaded = (Order, _version);

            Handler = new ConfirmGoodsReceiptCommandHandler(
                User, Receipts, Orders, Inventories, Clock);
        }

        public ConfirmGoodsReceiptCommand Command(int quantity)
        {
            return new ConfirmGoodsReceiptCommand(
                Guid.NewGuid(), Order.Id, _version,
                new[] { new ConfirmGoodsReceiptLineCommand(Order.Lines[0].Id, quantity) });
        }
    }
}