namespace StockFlow.Application.Inventory.MonitorLowStock
{
    public sealed record LowStockInventoryDto(
        Guid InventoryId,
        Guid ProductId,
        Guid WarehouseId,
        int QuantityOnHand,
        int ReorderThreshold);
}
