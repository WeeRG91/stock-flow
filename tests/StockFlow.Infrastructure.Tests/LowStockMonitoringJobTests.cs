using System.Reflection;
using Hangfire;
using Microsoft.Extensions.Logging.Abstractions;
using StockFlow.Application.Abstractions.Time;
using StockFlow.Application.Inventory.MonitorLowStock;
using StockFlow.Infrastructure.BackgroundJobs;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Tests;

public class LowStockMonitoringJobTests
{
    private static (
        LowStockMonitoringJob Job,
        TestReader Reader,
        TestStore Store) CreateSetup()
    {
        var reader = new TestReader();
        var store = new TestStore();

        var handler = new MonitorLowStockCommandHandler(
            new GetLowStockInventoriesQueryHandler(reader),
            store,
            new TestClock());

        var job = new LowStockMonitoringJob(
            handler,
            NullLogger<LowStockMonitoringJob>.Instance);

        return (job, reader, store);
    }

    [Fact]
    public async Task ExecuteAsync_InvokesMonitoringAndForwardsCancellation()
    {
        var setup = CreateSetup();
        using var cancellation = new CancellationTokenSource();

        await setup.Job.ExecuteAsync(cancellation.Token);

        Assert.Equal(1, setup.Reader.CallCount);
        Assert.Equal(1, setup.Store.CallCount);
        Assert.Equal(cancellation.Token, setup.Reader.ReceivedToken);
        Assert.Equal(cancellation.Token, setup.Store.ReceivedToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_WhenMonitoringFails_PropagatesFailure(
        bool storeFails)
    {
        var setup = CreateSetup();
        var failure = new InvalidOperationException("Simulated failure.");

        if (storeFails)
            setup.Store.Failure = failure;
        else
            setup.Reader.Failure = failure;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => setup.Job.ExecuteAsync());

        Assert.Same(failure, exception);
        Assert.Equal(storeFails ? 1 : 0, setup.Store.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlreadyCancelled_DoesNotStartMonitoring()
    {
        var setup = CreateSetup();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => setup.Job.ExecuteAsync(cancellation.Token));

        Assert.Equal(0, setup.Reader.CallCount);
        Assert.Equal(0, setup.Store.CallCount);
    }

    [Fact]
    public void ExecuteAsync_DeclaresExpectedRetryPolicy()
    {
        var method = typeof(LowStockMonitoringJob)
            .GetMethod(nameof(LowStockMonitoringJob.ExecuteAsync));

        var policy = method!
            .GetCustomAttribute<AutomaticRetryAttribute>();

        Assert.NotNull(policy);
        Assert.Equal(3, policy.Attempts);
        Assert.Equal(new[] { 30, 120, 300 }, policy.DelaysInSeconds);
        Assert.Equal(
            AttemptsExceededAction.Fail,
            policy.OnAttemptsExceeded);
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow =>
            new(2026, 10, 17, 10, 0, 0, TimeSpan.Zero);
    }

    private sealed class TestReader : IInventoryMonitoringReader
    {
        public int CallCount { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }
        public Exception? Failure { get; set; }

        public Task<IReadOnlyList<InventoryEntity>>
            GetMonitoredInventoriesAsync(
                CancellationToken cancellationToken = default)
        {
            CallCount++;
            ReceivedToken = cancellationToken;

            if (Failure is not null)
            {
                return Task.FromException<IReadOnlyList<InventoryEntity>>(
                    Failure);
            }

            return Task.FromResult<IReadOnlyList<InventoryEntity>>(
                Array.Empty<InventoryEntity>());
        }
    }

    private sealed class TestStore : ILowStockAlertStore
    {
        public int CallCount { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }
        public Exception? Failure { get; set; }

        public Task<LowStockAlertSyncResult> SynchronizeAsync(
            IReadOnlyCollection<Guid> lowStockInventoryIds,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ReceivedToken = cancellationToken;

            if (Failure is not null)
            {
                return Task.FromException<LowStockAlertSyncResult>(
                    Failure);
            }

            return Task.FromResult(
                new LowStockAlertSyncResult(0, 0, 0, 0));
        }
    }
}