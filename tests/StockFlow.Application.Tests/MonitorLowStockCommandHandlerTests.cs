using StockFlow.Application.Inventory.MonitorLowStock;
using StockFlow.Application.Tests.Fakes;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Tests;

public class MonitorLowStockCommandHandlerTests
{
    private static (
        MonitorLowStockCommandHandler Handler,
        FakeInventoryMonitoringReader Reader,
        FakeLowStockAlertStore Store,
        FakeClock Clock) CreateSetup()
    {
        var reader = new FakeInventoryMonitoringReader();
        var store = new FakeLowStockAlertStore();
        var clock = new FakeClock();

        var handler = new MonitorLowStockCommandHandler(
            new GetLowStockInventoriesQueryHandler(reader),
            store,
            clock);

        return (handler, reader, store, clock);
    }

    [Theory]
    [InlineData("queryHandler")]
    [InlineData("alerts")]
    [InlineData("clock")]
    public void Constructor_WithNullDependency_Throws(string dependency)
    {
        var queryHandler = new GetLowStockInventoriesQueryHandler(
            new FakeInventoryMonitoringReader());
        var store = new FakeLowStockAlertStore();
        var clock = new FakeClock();

        var exception = Assert.Throws<ArgumentNullException>(() =>
            new MonitorLowStockCommandHandler(
                dependency == "queryHandler" ? null! : queryHandler,
                dependency == "alerts" ? null! : store,
                dependency == "clock" ? null! : clock));

        Assert.Equal(dependency, exception.ParamName);
    }

    [Fact]
    public async Task HandleAsync_SynchronizesDistinctLowIdsAndReturnsResult()
    {
        var setup = CreateSetup();

        var low = new InventoryEntity(Guid.NewGuid(), Guid.NewGuid());
        low.SetReorderThreshold(10);

        var sufficient = new InventoryEntity(
            Guid.NewGuid(), Guid.NewGuid());
        sufficient.SetReorderThreshold(10);
        sufficient.ReceiveStock(10, "Test receipt", setup.Clock.UtcNow);

        var disabled = new InventoryEntity(
            Guid.NewGuid(), Guid.NewGuid());

        setup.Reader.Inventories = new[]
        {
            low, sufficient, disabled, low
        };
        setup.Store.Result = new LowStockAlertSyncResult(1, 0, 0, 1);

        using var cancellation = new CancellationTokenSource();

        var result = await setup.Handler.HandleAsync(
            new MonitorLowStockCommand(),
            cancellation.Token);

        Assert.Same(setup.Store.Result, result);
        Assert.Equal(1, setup.Reader.CallCount);
        Assert.Equal(1, setup.Store.CallCount);
        Assert.Equal(low.Id, Assert.Single(setup.Store.ReceivedInventoryIds));
        Assert.Equal(setup.Clock.UtcNow, setup.Store.ReceivedTimestamp);
        Assert.Equal(cancellation.Token, setup.Reader.ReceivedToken);
        Assert.Equal(cancellation.Token, setup.Store.ReceivedToken);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyScan_StillSynchronizes()
    {
        var setup = CreateSetup();

        await setup.Handler.HandleAsync(new MonitorLowStockCommand());

        Assert.Equal(1, setup.Store.CallCount);
        Assert.Empty(setup.Store.ReceivedInventoryIds);
    }

    [Fact]
    public async Task HandleAsync_WithNullCommand_DoesNotReadOrSynchronize()
    {
        var setup = CreateSetup();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => setup.Handler.HandleAsync(null!));

        Assert.Equal(0, setup.Reader.CallCount);
        Assert.Equal(0, setup.Store.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenAlreadyCancelled_DoesNotReadOrSynchronize()
    {
        var setup = CreateSetup();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => setup.Handler.HandleAsync(
                new MonitorLowStockCommand(),
                cancellation.Token));

        Assert.Equal(0, setup.Reader.CallCount);
        Assert.Equal(0, setup.Store.CallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_WithInvalidClock_DoesNotReadOrSynchronize(
        bool useDefault)
    {
        var setup = CreateSetup();
        setup.Clock.UtcNow = useDefault
            ? default
            : setup.Clock.UtcNow.ToOffset(TimeSpan.FromHours(2));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => setup.Handler.HandleAsync(new MonitorLowStockCommand()));

        Assert.Equal(0, setup.Reader.CallCount);
        Assert.Equal(0, setup.Store.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenReadFails_DoesNotSynchronize()
    {
        var setup = CreateSetup();
        var failure = new InvalidOperationException("Read failed.");
        setup.Reader.ReadException = failure;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => setup.Handler.HandleAsync(new MonitorLowStockCommand()));

        Assert.Same(failure, exception);
        Assert.Equal(0, setup.Store.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenSynchronizationFails_PropagatesFailure()
    {
        var setup = CreateSetup();
        var failure = new InvalidOperationException("Synchronization failed.");
        setup.Store.SynchronizationException = failure;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => setup.Handler.HandleAsync(new MonitorLowStockCommand()));

        Assert.Same(failure, exception);
        Assert.Equal(1, setup.Reader.CallCount);
        Assert.Equal(1, setup.Store.CallCount);
    }
}