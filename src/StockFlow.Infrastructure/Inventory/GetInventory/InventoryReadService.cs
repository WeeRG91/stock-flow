using Microsoft.EntityFrameworkCore;
using StockFlow.Application.Inventory.GetInventory;
using StockFlow.Infrastructure.Persistence;

namespace StockFlow.Infrastructure.Inventory.GetInventory
{
    public sealed class InventoryReadService : IInventoryReadService
    {
        private readonly StockFlowDbContext _context;

        public InventoryReadService(StockFlowDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        public async Task<GetInventoryResult> GetInventoryAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var offset = checked(
                (int)(((long)pageNumber - 1) * pageSize));

            var balances =
                from inventory in _context.Inventories.AsNoTracking()
                join product in _context.Products
                    on inventory.ProductId equals product.Id
                join warehouse in _context.Warehouses
                    on inventory.WarehouseId equals warehouse.Id
                select new
                {
                    InventoryId = inventory.Id,
                    ProductId = product.Id,
                    ProductName = product.Name,
                    product.Sku,
                    WarehouseId = warehouse.Id,
                    WarehouseName = warehouse.Name,
                    WarehouseCode = warehouse.Code,
                    inventory.QuantityOnHand
                };

            var totalCount = await balances.CountAsync(cancellationToken);

            var items = await balances
                .OrderBy(balance => balance.ProductName)
                .ThenBy(balance => balance.WarehouseName)
                .ThenBy(balance => balance.InventoryId)
                .Skip(offset)
                .Take(pageSize)
                .Select(balance => new InventoryBalanceDto(
                    balance.InventoryId,
                    balance.ProductId,
                    balance.ProductName,
                    balance.Sku,
                    balance.WarehouseId,
                    balance.WarehouseName,
                    balance.WarehouseCode,
                    balance.QuantityOnHand))
                .ToListAsync(cancellationToken);

            return new GetInventoryResult(
                items,
                pageNumber,
                pageSize,
                totalCount);
        }
    }
}
