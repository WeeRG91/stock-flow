using Microsoft.EntityFrameworkCore;
using StockFlow.Application.Inventory.MonitorLowStock;
using StockFlow.Infrastructure.Persistence;

namespace StockFlow.Infrastructure.Inventory.MonitorLowStock
{
    public sealed class InventoryMonitoringReader : IInventoryMonitoringReader
    {
        private readonly StockFlowDbContext _context;

        public InventoryMonitoringReader(StockFlowDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        public async Task<IReadOnlyList<Domain.Entities.Inventory>> GetMonitoredInventoriesAsync(CancellationToken cancellationToken = default)
        {
            var inventories =
                from inventory in _context.Inventories.AsNoTracking()
                join product in _context.Products
                    on inventory.ProductId equals product.Id
                join warehouse in _context.Warehouses
                    on inventory.WarehouseId equals warehouse.Id
                where inventory.ReorderThreshold != null
                    && product.IsActive
                    && warehouse.IsActive
                select inventory;

            return await inventories
                .OrderBy(inventory => inventory.Id)
                .ToListAsync(cancellationToken);
        }
    }
}
