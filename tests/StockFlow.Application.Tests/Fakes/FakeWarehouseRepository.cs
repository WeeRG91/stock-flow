using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Domain.Entities;

namespace StockFlow.Application.Tests.Fakes
{
    public sealed class FakeWarehouseRepository : IWarehouseRepository
    {
        private readonly Dictionary<Guid, Warehouse> _warehouses = new();

        public int GetByIdCallCount { get; private set; }

        public void Add(Warehouse warehouse)
        {
            _warehouses.Add(warehouse.Id, warehouse);
        }

        public Task<Warehouse?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetByIdCallCount++;

            _warehouses.TryGetValue(id, out var warehouse);

            return Task.FromResult<Warehouse?>(warehouse);
        }
    }
}