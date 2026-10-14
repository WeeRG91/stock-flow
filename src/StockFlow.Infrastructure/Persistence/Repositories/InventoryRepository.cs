using Microsoft.EntityFrameworkCore;
using StockFlow.Application.Abstractions.Persistence;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Persistence.Repositories
{
    public sealed class InventoryRepository : IInventoryRepository
    {
        private readonly StockFlowDbContext _context;
        private bool _saveFailed;

        public InventoryRepository(StockFlowDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        public Task<InventoryEntity?> GetByProductAndWarehouseAsync(
            Guid productId,
            Guid warehouseId,
            CancellationToken cancellationToken = default)
        {
            return _context.Inventories
                .AsTracking()
                .SingleOrDefaultAsync(
                    inventory =>
                        inventory.ProductId ==  productId &&
                        inventory.WarehouseId == warehouseId,
                        cancellationToken);
        }

        public async Task SaveAsync(InventoryEntity inventory, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(inventory);
            cancellationToken.ThrowIfCancellationRequested();

            if (_saveFailed)
            {
                throw new InvalidOperationException(
                    "A previous save failed. Discard this scope and its DbContext.");
            }

            if (_context.Database.CurrentTransaction is not null ||
                System.Transactions.Transaction.Current is not null)
            {
                throw new InvalidOperationException(
                    "This save operation must own its commit boundary.");
            }

            var movements = inventory.PendingMovements.ToArray();
            var movementIds = movements.Select(movement => movement.Id).ToArray();

            try
            {
                StageInventory(inventory);

                foreach (var movement in movements)
                {
                    if (_context.Entry(movement).State != EntityState.Detached)
                    {
                        throw new InvalidOperationException(
                            "A pending movement is already tracked. Discard this scope.");
                    }

                    _context.StockMovements.Add(movement);
                }

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                _saveFailed = true;
                throw;
            }

            inventory.AcknowledgePersistedMovements(movementIds);
        }

        private void StageInventory(InventoryEntity inventory)
        {
            var entry = _context.Entry(inventory);

            switch (entry.State)
            {
                case EntityState.Detached:
                    _context.Inventories.Add(inventory);
                    break;

                case EntityState.Added:
                case EntityState.Unchanged:
                case EntityState.Modified:
                    break;

                default:
                    throw new InvalidOperationException(
                    "Cannot save inventory in its current tracking state.");
            }
        }
    }
}
