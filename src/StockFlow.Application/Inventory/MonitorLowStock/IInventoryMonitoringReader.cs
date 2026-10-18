using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Inventory.MonitorLowStock
{
    public interface IInventoryMonitoringReader
    {
        /// <summary>
        /// Reads existing inventories with a configured threshold
        /// whose product and warehouse are both active.
        /// Includes balances that are not currently low.
        /// </summary>
        Task<IReadOnlyList<InventoryEntity>> GetMonitoredInventoriesAsync(
            CancellationToken cancellationToken = default);
    }
}
