using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Abstractions.Persistence
{
    public interface IInventoryRepository
    {
        Task<InventoryEntity?> GetByProductAndWarehouseAsync(
            Guid productId,
            Guid warehouseId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Inserts or updates the inventory balance and persists its
        /// pending movements atomically, without duplicating movements
        /// already saved.
        /// </summary>
        Task SaveAsync(
            InventoryEntity inventory,
            CancellationToken cancellationToken = default);
    }
}
