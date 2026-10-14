namespace StockFlow.Application.Inventory.ReceiveStock
{
    public sealed record ReceiveStockCommand(
        Guid ProductId,
        Guid WarehouseId,
        int Quantity);
}
