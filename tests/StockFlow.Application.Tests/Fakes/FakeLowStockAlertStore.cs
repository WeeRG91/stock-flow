using StockFlow.Application.Inventory.MonitorLowStock;

namespace StockFlow.Application.Tests.Fakes
{
    public sealed class FakeLowStockAlertStore : ILowStockAlertStore
    {
        public int CallCount { get; private set; }

        public Guid[] ReceivedInventoryIds { get; private set; } = [];

        public DateTimeOffset ReceivedTimestamp { get; private set; }

        public CancellationToken ReceivedToken { get; private set; }

        public LowStockAlertSyncResult Result { get; set; } =
            new(0, 0, 0, 0);

        public Exception? SynchronizationException { get; set; }

        public Task<LowStockAlertSyncResult> SynchronizeAsync(
            IReadOnlyCollection<Guid> lowStockInventoryIds,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ReceivedInventoryIds = lowStockInventoryIds.ToArray();
            ReceivedTimestamp = evaluatedAt;
            ReceivedToken = cancellationToken;

            if (SynchronizationException is not null)
            {
                return Task.FromException<LowStockAlertSyncResult>(
                    SynchronizationException);
            }

            return Task.FromResult(Result);
        }
    }
}