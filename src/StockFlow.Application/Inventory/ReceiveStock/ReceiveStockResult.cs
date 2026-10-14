namespace StockFlow.Application.Inventory.ReceiveStock
{
    public sealed record ReceiveStockResult(
        Guid InventoryId,
        int QuantityOnHand);
}
