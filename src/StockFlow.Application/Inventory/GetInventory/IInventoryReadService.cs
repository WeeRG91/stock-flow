namespace StockFlow.Application.Inventory.GetInventory
{
    public interface IInventoryReadService
    {
        Task<GetInventoryResult> GetInventoryAsync(
            int pageNumber,
            int pageSize,
            Guid? productId,
            Guid? warehouseId,
            CancellationToken cancellationToken = default);
    }
}
