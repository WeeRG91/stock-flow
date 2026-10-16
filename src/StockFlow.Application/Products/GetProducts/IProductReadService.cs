namespace StockFlow.Application.Products.GetProducts
{
    public interface IProductReadService
    {
        Task<IReadOnlyList<ProductDto>> GetProductsAsync(
            CancellationToken cancellationToken = default);
    }
}
