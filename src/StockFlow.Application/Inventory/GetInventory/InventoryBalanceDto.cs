namespace StockFlow.Application.Inventory.GetInventory
{
    public sealed record InventoryBalanceDto(
        Guid InventoryId,
        Guid ProductId,
        string ProductName,
        string Sku,
        Guid WarehouseId,
        string WarehouseName,
        string WarehouseCode,
        int QuantityOnHand);
}
