namespace StockFlow.Application.Warehouses.GetWarehouses
{
    public interface IWarehouseReadService
    {
        Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(
            CancellationToken cancellationToken = default);
    }
}
