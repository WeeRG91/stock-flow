using Microsoft.EntityFrameworkCore;
using StockFlow.Infrastructure.Persistence;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Development
{
    public static class DevelopmentMonitoringSetup
    {
        public static async Task<InventoryEntity> ConfigureAsync(
            StockFlowDbContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var candidates =
                from inventory in context.Inventories.AsTracking()
                join product in context.Products
                    on inventory.ProductId equals product.Id
                join warehouse in context.Warehouses
                    on inventory.WarehouseId equals warehouse.Id
                where product.Sku.StartsWith("DEMO-")
                    && warehouse.Code.StartsWith("DEMO-")
                    && product.IsActive
                    && warehouse.IsActive
                orderby product.Sku, warehouse.Code, inventory.Id
                select inventory;

            var selectedInventory = await candidates.FirstOrDefaultAsync(
                cancellationToken);

            if (selectedInventory is null)
            {
                throw new InvalidOperationException(
                    "Receive stock for a demo product and warehouse first.");
            }

            var threshold = checked(selectedInventory.QuantityOnHand + 5);
            selectedInventory.SetReorderThreshold(threshold);

            await context.SaveChangesAsync(cancellationToken);

            return selectedInventory;
        }
    }
}