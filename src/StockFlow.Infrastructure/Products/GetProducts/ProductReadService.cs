using Microsoft.EntityFrameworkCore;
using StockFlow.Application.Products.GetProducts;
using StockFlow.Infrastructure.Persistence;

namespace StockFlow.Infrastructure.Products.GetProducts
{
    public class ProductReadService : IProductReadService
    {
        private readonly StockFlowDbContext _context;

        public ProductReadService(StockFlowDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }
        public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .AsNoTracking()
                .OrderBy(product => product.Name)
                .ThenBy(product => product.Id)
                .Select(product => new ProductDto(
                    product.Id,
                    product.Name,
                    product.Sku,
                    product.Description,
                    product.IsActive))
                .ToListAsync(cancellationToken);
        }
    }
}
