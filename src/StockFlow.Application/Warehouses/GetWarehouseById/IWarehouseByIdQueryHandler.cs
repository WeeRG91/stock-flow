using StockFlow.Application.Exceptions;
using StockFlow.Application.Warehouses.GetWarehouses;

namespace StockFlow.Application.Warehouses.GetWarehouseById
{
    public sealed class GetWarehouseByIdQueryHandler
    {
        private readonly IWarehouseReadService _warehouses;

        public GetWarehouseByIdQueryHandler(
            IWarehouseReadService warehouses)
        {
            ArgumentNullException.ThrowIfNull(warehouses);
            _warehouses = warehouses;
        }

        public async Task<WarehouseDto> HandleAsync(
            GetWarehouseByIdQuery query,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();

            if (query.WarehouseId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(query.WarehouseId),
                    "Warehouse ID must not be empty.");
            }

            var warehouse = await _warehouses.GetByIdAsync(
                query.WarehouseId,
                cancellationToken);

            if (warehouse is null)
            {
                throw new ResourceNotFoundException(
                    "Warehouse",
                    query.WarehouseId);
            }

            return warehouse;
        }
    }
}