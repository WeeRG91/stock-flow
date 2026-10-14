using Microsoft.EntityFrameworkCore;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Domain.Entities;

namespace StockFlow.Infrastructure.Persistence.Repositories
{
    public sealed class ProductRepository : IProductRepository
    {
        private readonly StockFlowDbContext _context;

        public ProductRepository(StockFlowDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return _context.Products
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    product => product.Id == id,
                    cancellationToken);
        }
    }
}
