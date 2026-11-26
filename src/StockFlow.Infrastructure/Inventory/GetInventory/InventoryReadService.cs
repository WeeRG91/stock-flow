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

        public async Task<GetInventoryResult> GetInventoryAsync(
            int pageNumber,
            int pageSize,
            Guid? productId,
            Guid? warehouseId,
            CancellationToken cancellationToken = default)
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
                    inventory.QuantityOnHand,
                    inventory.QuantityReserved
                };

            if (productId.HasValue)
            {
                balances = balances.Where(
                    balance => balance.ProductId == productId.Value);
            }

            if (warehouseId.HasValue)
            {
                balances = balances.Where(
                    balance => balance.WarehouseId == warehouseId.Value);
            }

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
                    balance.QuantityOnHand,
                    balance.QuantityReserved))
                .ToListAsync(cancellationToken);

            return new GetInventoryResult(
                items,
                pageNumber,
                pageSize,
                totalCount);
        }
    }
}
