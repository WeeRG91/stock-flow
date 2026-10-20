using Microsoft.EntityFrameworkCore;
using StockFlow.Application.Suppliers;
using StockFlow.Application.Suppliers.GetSuppliers;
using StockFlow.Infrastructure.Persistence;

namespace StockFlow.Infrastructure.Suppliers
{
    public sealed class SupplierReadService : ISupplierReadService
    {
        private readonly StockFlowDbContext _context;

        public SupplierReadService(StockFlowDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        public async Task<GetSuppliersResult> GetSuppliersAsync(int pageNumber, int pageSize, string? search, bool? isActive, CancellationToken cancellationToken = default)
        {
            var suppliers = _context.Suppliers.AsNoTracking();

            if (isActive.HasValue)
            {
                suppliers = suppliers.Where(
                    supplier => supplier.IsActive == isActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var escapedSearch = search
                    .Replace("\\", "\\\\")
                    .Replace("%", "\\%")
                    .Replace("_", "\\_");

                var pattern = $"%{escapedSearch}%";

                suppliers = suppliers.Where(supplier =>
                    EF.Functions.ILike(supplier.Name, pattern, "\\") ||
                    EF.Functions.ILike(supplier.Code, pattern, "\\"));
            }

            var totalCount = await suppliers.CountAsync(cancellationToken);

            var offset = checked((int)(((long)pageNumber - 1) * pageSize));

            var items = await suppliers
               .OrderBy(supplier => supplier.Code)
               .ThenBy(supplier => supplier.Id)
               .Skip(offset)
               .Take(pageSize)
               .Select(supplier => new SupplierDto(
                   supplier.Id,
                   supplier.Code,
                   supplier.Name,
                   supplier.ContactEmail,
                   supplier.PhoneNumber,
                   supplier.IsActive))
               .ToListAsync(cancellationToken);

            return new GetSuppliersResult(
                items,
                pageNumber,
                pageSize,
                totalCount);
        }

        public Task<SupplierDto?> GetSupplierByIdAsync(
            Guid supplierId,
            CancellationToken cancellationToken = default)
        {
            return _context.Suppliers
                .AsNoTracking()
                .Where(supplier => supplier.Id == supplierId)
                .Select(supplier => new SupplierDto(
                    supplier.Id,
                    supplier.Code,
                    supplier.Name,
                    supplier.ContactEmail,
                    supplier.PhoneNumber,
                    supplier.IsActive))
                .SingleOrDefaultAsync(cancellationToken);
        }
    }
}
