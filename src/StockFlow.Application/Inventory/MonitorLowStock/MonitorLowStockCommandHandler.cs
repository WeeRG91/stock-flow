using StockFlow.Application.Abstractions.Time;

namespace StockFlow.Application.Inventory.MonitorLowStock
{
    public sealed class MonitorLowStockCommandHandler
    {
        private readonly GetLowStockInventoriesQueryHandler _queryHandler;
        private readonly ILowStockAlertStore _alerts;
        private readonly IClock _clock;

        public MonitorLowStockCommandHandler(
            GetLowStockInventoriesQueryHandler queryHandler,
            ILowStockAlertStore alerts,
            IClock clock)
        {
            ArgumentNullException.ThrowIfNull(queryHandler);
            ArgumentNullException.ThrowIfNull(alerts);
            ArgumentNullException.ThrowIfNull(clock);

            _queryHandler = queryHandler;
            _alerts = alerts;
            _clock = clock;
        }

        public async Task<LowStockAlertSyncResult> HandleAsync(
            MonitorLowStockCommand command,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            cancellationToken.ThrowIfCancellationRequested();

            var evaluatedAt = _clock.UtcNow;

            if (evaluatedAt == default ||
                evaluatedAt.Offset != TimeSpan.Zero)
            {
                throw new InvalidOperationException(
                    "Monitoring requires a non-default UTC timestamp.");
            }

            var lowStock = await _queryHandler.HandleAsync(
                new GetLowStockInventoriesQuery(),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var inventoryIds = lowStock
                .Select(item => item.InventoryId)
                .Distinct()
                .ToArray();

            return await _alerts.SynchronizeAsync(
                inventoryIds,
                evaluatedAt,
                cancellationToken);
        }
    }
}