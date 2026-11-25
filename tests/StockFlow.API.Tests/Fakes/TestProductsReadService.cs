using StockFlow.Application.Products.GetProducts;

namespace StockFlow.API.Tests.Fakes
{
    public sealed class TestProductReadService : IProductReadService
    {
        public IReadOnlyList<ProductDto> Products { get; set; } =
            Array.Empty<ProductDto>();

        public Exception? ReadException { get; set; }
        public int CallCount { get; private set; }

        public Task<ProductDto?> GetByIdAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;

            if (ReadException is not null)
            {
                throw ReadException;
            }

            return Task.FromResult(
                Products.SingleOrDefault(product => product.Id == productId));
        }

        public Task<IReadOnlyList<ProductDto>> GetProductsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;

            if (ReadException is not null)
            {
                throw ReadException;
            }

            return Task.FromResult(Products);
        }
    }
}
