using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Domain.Entities;

namespace StockFlow.Application.Tests.Fakes
{
    public sealed class FakeProductRepository : IProductRepository
    {
        private readonly Dictionary<Guid, Product> _products = new();

        public int GetByIdCallCount { get; private set; }

        public void Add(Product product)
        {
            _products.Add(product.Id, product);
        }

        public Task<Product?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetByIdCallCount++;

            _products.TryGetValue(id, out var product);

            return Task.FromResult<Product?>(product);
        }
    }
}