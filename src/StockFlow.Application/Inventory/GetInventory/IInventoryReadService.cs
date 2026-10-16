namespace StockFlow.Application.Inventory.GetInventory
{
    public interface IInventoryReadService
    {
        Task<GetInventoryResult> GetInventoryAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);
    }
}
