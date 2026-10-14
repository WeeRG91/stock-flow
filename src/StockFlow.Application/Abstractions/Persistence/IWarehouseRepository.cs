using StockFlow.Domain.Entities;

namespace StockFlow.Application.Abstractions.Persistence
{
    public interface IWarehouseRepository
    {
        Task<Warehouse?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default);
    }
}
