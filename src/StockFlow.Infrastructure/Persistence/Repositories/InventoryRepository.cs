using Microsoft.EntityFrameworkCore;
using StockFlow.Application.Abstractions.Persistence;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Persistence.Repositories
{
    public sealed class InventoryRepository : IInventoryRepository
    {
        private readonly StockFlowDbContext _context;

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

        public Task SaveAsync(InventoryEntity inventory, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "Inventory saving requires atomic balance and movement persistence, " +
                "which is not implemented yet.");
        }
    }
}
