namespace StockFlow.Application.Inventory.MonitorLowStock
{
    public sealed class GetLowStockInventoriesQueryHandler
    {
        private readonly IInventoryMonitoringReader _reader;

        public GetLowStockInventoriesQueryHandler(
            IInventoryMonitoringReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            _reader = reader;
        }

        public async Task<IReadOnlyList<LowStockInventoryDto>> HandleAsync(
            GetLowStockInventoriesQuery query,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();

            var inventories = await _reader.GetMonitoredInventoriesAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            return inventories
                .Where(inventory => inventory.IsLowStock)
                .OrderBy(inventory => inventory.WarehouseId)
                .ThenBy(inventory => inventory.ProductId)
                .ThenBy(inventory => inventory.Id)
                .Select(inventory => new LowStockInventoryDto(
                    inventory.Id,
                    inventory.ProductId,
                    inventory.WarehouseId,
                    inventory.QuantityOnHand,
                    inventory.ReorderThreshold!.Value))
                .ToArray();
        }
    }
}
