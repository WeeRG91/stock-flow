using StockFlow.Application.Exceptions;
using StockFlow.Application.Products.GetProducts;

namespace StockFlow.Application.Products.GetProductById
{
    public sealed class GetProductByIdQueryHandler
    {
        private readonly IProductReadService _products;

        public GetProductByIdQueryHandler(IProductReadService products)
        {
            ArgumentNullException.ThrowIfNull(products);
            _products = products;
        }

        public async Task<ProductDto> HandleAsync(
            GetProductByIdQuery query,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();

            if (query.ProductId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(query.ProductId),
                    "Product ID must not be empty.");
            }

            var product = await _products.GetByIdAsync(
                query.ProductId,
                cancellationToken);

            if (product is null)
            {
                throw new ResourceNotFoundException(
                    "Product",
                    query.ProductId);
            }

            return product;
        }
    }
}
