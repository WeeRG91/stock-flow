using StockFlow.Application.Warehouses.GetWarehouses;

namespace StockFlow.API.Tests.Fakes
{
    public sealed class TestWarehouseReadService : IWarehouseReadService
    {
        public IReadOnlyList<WarehouseDto> Warehouses { get; set; } =
            Array.Empty<WarehouseDto>();

        public Exception? ReadException { get; set; }
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;

            if (ReadException is not null)
            {
                throw ReadException;
            }

            return Task.FromResult(Warehouses);
        }
    }
}