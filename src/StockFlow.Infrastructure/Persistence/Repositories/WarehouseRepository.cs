using Microsoft.EntityFrameworkCore;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Domain.Entities;

namespace StockFlow.Infrastructure.Persistence.Repositories
{
    public sealed class WarehouseRepository : IWarehouseRepository
    {
        private readonly StockFlowDbContext _context;

        public WarehouseRepository(StockFlowDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        public Task<Warehouse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return _context.Warehouses
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    warehouse => warehouse.Id == id,
                    cancellationToken);
        }
    }
}
