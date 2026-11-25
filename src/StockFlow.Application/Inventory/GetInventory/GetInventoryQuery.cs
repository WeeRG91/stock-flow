namespace StockFlow.Application.Inventory.GetInventory
{
    public sealed record GetInventoryQuery(
        int PageNumber = 1,
        int PageSize = 20,
        Guid? ProductId = null,
        Guid? WarehouseId = null);
}
