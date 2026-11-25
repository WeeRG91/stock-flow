namespace StockFlow.Application.Warehouses.GetWarehouses
{
    public interface IWarehouseReadService
    {
        Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(
            CancellationToken cancellationToken = default);

        Task<WarehouseDto?> GetByIdAsync(
            Guid warehouseId,
            CancellationToken cancellationToken = default);
    }
}
