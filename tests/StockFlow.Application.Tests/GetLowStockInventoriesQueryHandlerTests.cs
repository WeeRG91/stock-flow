using StockFlow.Application.Inventory.MonitorLowStock;
using StockFlow.Application.Tests.Fakes;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Tests;

public class GetLowStockInventoriesQueryHandlerTests
{
    private static InventoryEntity CreateInventory(
        int quantity,
        int? threshold)
    {
        var inventory = new InventoryEntity(
            Guid.NewGuid(), Guid.NewGuid());

        if (threshold.HasValue)
        {
            inventory.SetReorderThreshold(threshold.Value);
        }

        if (quantity > 0)
        {
            inventory.ReceiveStock(
                quantity,
                "Test receipt",
                new DateTimeOffset(
                    2026, 10, 16, 10, 0, 0, TimeSpan.Zero));
        }

        return inventory;
    }

    [Fact]
    public void Constructor_WithNullReader_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GetLowStockInventoriesQueryHandler(null!));
    }

    [Fact]
    public async Task HandleAsync_WithNoInventories_ReturnsEmptyResult()
    {
        var reader = new FakeInventoryMonitoringReader();
        var handler = new GetLowStockInventoriesQueryHandler(reader);

        var result = await handler.HandleAsync(
            new GetLowStockInventoriesQuery());

        Assert.Empty(result);
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_ReturnsOnlyLowStockWithCorrectValues()
    {
        var low = CreateInventory(9, 10);
        var reader = new FakeInventoryMonitoringReader
        {
            Inventories = new[]
            {
                CreateInventory(0, null),
                CreateInventory(10, 10),
                low,
                CreateInventory(11, 10)
            }
        };
        var handler = new GetLowStockInventoriesQueryHandler(reader);

        var result = await handler.HandleAsync(
            new GetLowStockInventoriesQuery());

        var item = Assert.Single(result);
        Assert.Equal(low.Id, item.InventoryId);
        Assert.Equal(low.ProductId, item.ProductId);
        Assert.Equal(low.WarehouseId, item.WarehouseId);
        Assert.Equal(9, item.QuantityOnHand);
        Assert.Equal(10, item.ReorderThreshold);
    }

    [Fact]
    public async Task HandleAsync_RepeatedReads_DoNotModifyInventory()
    {
        var inventory = CreateInventory(5, 10);
        var movementsBefore = inventory.PendingMovements.ToArray();
        var reader = new FakeInventoryMonitoringReader
        {
            Inventories = new[] { inventory }
        };
        var handler = new GetLowStockInventoriesQueryHandler(reader);

        var first = await handler.HandleAsync(
            new GetLowStockInventoriesQuery());
        var second = await handler.HandleAsync(
            new GetLowStockInventoriesQuery());

        Assert.Single(first);
        Assert.Equal(first.ToArray(), second.ToArray());
        Assert.Equal(5, inventory.QuantityOnHand);
        Assert.Equal(10, inventory.ReorderThreshold);
        Assert.Equal(
            movementsBefore,
            inventory.PendingMovements.ToArray());
    }

    [Fact]
    public async Task HandleAsync_WithNullQuery_DoesNotRead()
    {
        var reader = new FakeInventoryMonitoringReader();
        var handler = new GetLowStockInventoriesQueryHandler(reader);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.HandleAsync(null!));

        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenAlreadyCancelled_DoesNotRead()
    {
        var reader = new FakeInventoryMonitoringReader();
        var handler = new GetLowStockInventoriesQueryHandler(reader);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(
                new GetLowStockInventoriesQuery(),
                cancellation.Token));

        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_ForwardsCancellationToken()
    {
        var reader = new FakeInventoryMonitoringReader();
        var handler = new GetLowStockInventoriesQueryHandler(reader);
        using var cancellation = new CancellationTokenSource();

        await handler.HandleAsync(
            new GetLowStockInventoriesQuery(),
            cancellation.Token);

        Assert.Equal(1, reader.CallCount);
        Assert.Equal(cancellation.Token, reader.ReceivedToken);
    }

    [Fact]
    public async Task HandleAsync_WhenReadFails_PropagatesFailure()
    {
        var failure = new InvalidOperationException(
            "Simulated read failure.");
        var reader = new FakeInventoryMonitoringReader
        {
            ReadException = failure
        };
        var handler = new GetLowStockInventoriesQueryHandler(reader);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                new GetLowStockInventoriesQuery()));

        Assert.Same(failure, exception);
    }
}