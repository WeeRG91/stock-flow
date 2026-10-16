namespace StockFlow.Application.Products.GetProducts
{
    public sealed class GetProductsQueryHandler
    {
        private readonly IProductReadService _products;

        public GetProductsQueryHandler(IProductReadService products)
        {
            ArgumentNullException.ThrowIfNull(products);
            _products = products;
        }

        public Task<IReadOnlyList<ProductDto>> HandleAsync(
            GetProductsQuery query,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();

            return _products.GetProductsAsync(cancellationToken);
        }
    }
}
