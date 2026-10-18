namespace StockFlow.Application.Inventory.MonitorLowStock
{
    public interface ILowStockAlertStore
    {
        /// <summary>
        /// Reconciles alerts against a complete successful low-stock scan.
        /// An empty collection resolves all currently active alerts.
        /// Persists all alert changes atomically.
        /// Repeated calls must not create duplicate alerts or replace
        /// timestamps on unchanged alert states.
        /// </summary>
        Task<LowStockAlertSyncResult> SynchronizeAsync(
            IReadOnlyCollection<Guid> lowStockInventoryIds,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken = default);
    }
}