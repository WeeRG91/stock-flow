using StockFlow.Application.Abstractions.Persistence;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Tests.Fakes
{
    public sealed class FakeInventoryRepository : IInventoryRepository
    {
        private readonly Dictionary<(Guid, Guid), InventoryEntity>
            _inventories = new();

        public int GetCallCount { get; private set; }
        public int SaveCallCount { get; private set; }
        public InventoryEntity? LastSavedInventory { get; private set; }

        public void Add(InventoryEntity inventory)
        {
            _inventories.Add(
                (inventory.ProductId, inventory.WarehouseId),
                inventory);
        }

        public Task<InventoryEntity?> GetByProductAndWarehouseAsync(
            Guid productId,
            Guid warehouseId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetCallCount++;

            _inventories.TryGetValue(
                (productId, warehouseId),
                out var inventory);

            return Task.FromResult<InventoryEntity?>(inventory);
        }

        public Task SaveAsync(
            InventoryEntity inventory,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCallCount++;

            _inventories[(inventory.ProductId, inventory.WarehouseId)] =
                inventory;

            LastSavedInventory = inventory;

            return Task.CompletedTask;
        }
    }
}