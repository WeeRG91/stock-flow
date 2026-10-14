using StockFlow.Domain.Entities;

namespace StockFlow.Application.Abstractions.Persistence
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default);
    }
}
