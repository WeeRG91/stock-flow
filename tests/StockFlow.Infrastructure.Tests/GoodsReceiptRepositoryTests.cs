using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using StockFlow.Infrastructure.Persistence;
using StockFlow.Infrastructure.Persistence.Repositories;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Tests;

public sealed class GoodsReceiptRepositoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveConfirmationAsync_StagesAllChangesInOneSave(
        bool existingBalance)
    {
        var scenario = new ConfirmationScenario();
        var originalVersion = Guid.NewGuid();

        var interceptor = new ConfirmationSaveInterceptor(context =>
        {
            var order = context.Entry(scenario.Order);
            var version = order.Property<Guid>("Version");
            Assert.True(version.IsModified);
            Assert.Equal(originalVersion, version.OriginalValue);
            Assert.NotEqual(originalVersion, version.CurrentValue);

            // The order was already partially received before this delivery.
            Assert.False(order.Property(item => item.Status).IsModified);
            Assert.Equal(PurchaseOrderStatus.PartiallyReceived, scenario.Order.Status);

            var orderLine = Assert.Single(context.ChangeTracker.Entries<PurchaseOrderLine>());
            var received = orderLine.Property(line => line.ReceivedQuantity);
            Assert.True(received.IsModified);
            Assert.Equal(2, received.OriginalValue);
            Assert.Equal(6, received.CurrentValue);

            Assert.Equal(EntityState.Added, context.Entry(scenario.Receipt).State);

            var receiptLine = Assert.Single(context.ChangeTracker.Entries<GoodsReceiptLine>());
            Assert.Equal(EntityState.Added, receiptLine.State);
            Assert.Equal(scenario.Receipt.Id, receiptLine.Property<Guid>("GoodsReceiptId").CurrentValue);

            var balance = Assert.Single(context.ChangeTracker.Entries<InventoryEntity>());
            Assert.Equal(existingBalance ? EntityState.Modified : EntityState.Added, balance.State);

            var movement = Assert.Single(context.ChangeTracker.Entries<StockMovement>());
            Assert.Equal(EntityState.Added, movement.State);
            Assert.Equal(scenario.Balance.Id, movement.Entity.InventoryId);
            Assert.Equal(4, movement.Entity.QuantityChange);
            Assert.Single(scenario.Balance.PendingMovements);
        });

        await using var context = CreateContext(interceptor);
        scenario.Prepare(context, originalVersion, existingBalance);
        var repository = new GoodsReceiptRepository(context);

        await repository.SaveConfirmationAsync(
            scenario.Receipt, scenario.Order, new[] { scenario.Balance });

        Assert.Equal(1, interceptor.CallCount);
        Assert.Empty(scenario.Balance.PendingMovements);
    }

    [Fact]
    public async Task SaveConfirmationAsync_OnConcurrencyFailure_KeepsMovementsPending()
    {
        var scenario = new ConfirmationScenario();
        var interceptor = new ConfirmationSaveInterceptor(_ =>
            throw new DbUpdateConcurrencyException("Simulated stale update."));

        await using var context = CreateContext(interceptor);
        scenario.Prepare(context, Guid.NewGuid(), existingBalance: true);
        var repository = new GoodsReceiptRepository(context);
        var inventories = new[] { scenario.Balance };

        var conflict = await Assert.ThrowsAsync<PurchaseOrderConflictException>(
            () => repository.SaveConfirmationAsync(
                scenario.Receipt, scenario.Order, inventories));

        Assert.Equal(scenario.Order.Id, conflict.PurchaseOrderId);
        Assert.Equal(4, Assert.Single(scenario.Balance.PendingMovements).QuantityChange);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.SaveConfirmationAsync(
                scenario.Receipt, scenario.Order, inventories));

        Assert.Equal(1, interceptor.CallCount);
    }

    private static StockFlowDbContext CreateContext(SaveChangesInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<StockFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=unused_receipt_test")
            .AddInterceptors(interceptor)
            .Options;

        return new StockFlowDbContext(options);
    }

    private sealed class ConfirmationSaveInterceptor(
        Action<StockFlowDbContext> inspect) : SaveChangesInterceptor
    {
        public int CallCount { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            inspect(Assert.IsType<StockFlowDbContext>(eventData.Context));
            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
        }
    }

    private sealed class ConfirmationScenario
    {
        public PurchaseOrder Order { get; }
        public GoodsReceipt Receipt { get; }
        public InventoryEntity Balance { get; }

        public ConfirmationScenario()
        {
            var timestamp = new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
            var productId = Guid.NewGuid();
            var warehouseId = Guid.NewGuid();
            var receiverId = Guid.NewGuid();

            Order = new PurchaseOrder(
                "PO-RECEIPT-TEST", Guid.NewGuid(), "SUP-01", "Demo supplier",
                warehouseId, "BRU", "Brussels", timestamp.AddMinutes(-4), Guid.NewGuid());

            var lineId = Order.AddLine(productId, "CHAIR", "Office chair", 10, 25m);
            Order.Submit(timestamp.AddMinutes(-3));
            Order.Approve(timestamp.AddMinutes(-2), Guid.NewGuid());
            Order.ApplyReceipt(new GoodsReceipt(
                Guid.NewGuid(), Order.Id, timestamp.AddMinutes(-1), receiverId,
                new[] { new GoodsReceiptLine(lineId, 2) }));

            Receipt = new GoodsReceipt(
                Guid.NewGuid(), Order.Id, timestamp, receiverId,
                new[] { new GoodsReceiptLine(lineId, 4) });

            Balance = new InventoryEntity(productId, warehouseId);
        }

        public void Prepare(
            StockFlowDbContext context, Guid originalVersion, bool existingBalance)
        {
            var version = context.PurchaseOrders.Attach(Order).Property<Guid>("Version");
            version.CurrentValue = originalVersion;
            version.OriginalValue = originalVersion;
            version.IsModified = false;

            if (existingBalance)
            {
                context.Inventories.Attach(Balance);
            }

            Order.ApplyReceipt(Receipt);
            Balance.ReceiveStock(4, $"Goods receipt {Receipt.Id}", Receipt.ConfirmedAt);
        }
    }
}