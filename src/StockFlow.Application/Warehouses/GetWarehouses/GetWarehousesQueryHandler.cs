namespace StockFlow.Application.Warehouses.GetWarehouses
{
    public sealed class GetWarehousesQueryHandler
    {
        private readonly IWarehouseReadService _warehouse;

        public GetWarehousesQueryHandler(IWarehouseReadService warehouse)
        {
            ArgumentNullException.ThrowIfNull(warehouse);
            _warehouse = warehouse;
        }

        public Task<IReadOnlyList<WarehouseDto>> HandleAsync(
            GetWarehousesQuery query,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();

            return _warehouse.GetWarehousesAsync(cancellationToken);
        }
    }
}
