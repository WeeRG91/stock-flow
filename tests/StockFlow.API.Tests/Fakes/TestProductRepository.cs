using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Domain.Entities;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.API.Tests.Fakes
{
    public sealed class TestProductRepository : IProductRepository
    {
        public Product? Product { get; set; }

        public Task<Product?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<Product?>(
                Product?.Id == id ? Product : null);
        }
    }

    public sealed class TestWarehouseRepository : IWarehouseRepository
    {
        public Warehouse? Warehouse { get; set; }

        public Task<Warehouse?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<Warehouse?>(
                Warehouse?.Id == id ? Warehouse : null);
        }
    }

    public sealed class TestInventoryRepository : IInventoryRepository
    {
        public InventoryEntity? Inventory { get; set; }
        public InventoryEntity? LastSavedInventory { get; private set; }
        public Exception? SaveException { get; set; }
        public int SaveCallCount { get; private set; }
        public int GetCallCount { get; private set; }

        public Task<InventoryEntity?> GetByProductAndWarehouseAsync(
            Guid productId,
            Guid warehouseId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetCallCount++;

            var inventory = Inventory;

            return Task.FromResult<InventoryEntity?>(
                inventory?.ProductId == productId &&
                inventory.WarehouseId == warehouseId
                    ? inventory
                    : null);
        }

        public Task SaveAsync(
            InventoryEntity inventory,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCallCount++;

            if (SaveException is not null)
            {
                throw SaveException;
            }

            LastSavedInventory = inventory;
            Inventory = inventory;

            return Task.CompletedTask;
        }
    }
}