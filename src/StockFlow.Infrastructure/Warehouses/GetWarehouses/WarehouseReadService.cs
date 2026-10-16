using Microsoft.EntityFrameworkCore;
using StockFlow.Application.Warehouses.GetWarehouses;
using StockFlow.Infrastructure.Persistence;

namespace StockFlow.Infrastructure.Warehouses.GetWarehouses
{
    public sealed class WarehouseReadService : IWarehouseReadService
    {
        private readonly StockFlowDbContext _context;

        public WarehouseReadService(StockFlowDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        public async Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Warehouses
                .AsNoTracking()
                .OrderBy(warehouse => warehouse.Name)
                .ThenBy(warehouse => warehouse.Id)
                .Select(warehouse => new WarehouseDto(
                    warehouse.Id,
                    warehouse.Code,
                    warehouse.Name,
                    warehouse.Location,
                    warehouse.IsActive))
                .ToListAsync(cancellationToken);
        }
    }
}
