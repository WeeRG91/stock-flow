namespace StockFlow.Application.Inventory.GetInventory
{
    public sealed record GetInventoryResult(
        IReadOnlyList<InventoryBalanceDto> Items,
        int PageNumber,
        int PageSize,
        int TotalCount);
}