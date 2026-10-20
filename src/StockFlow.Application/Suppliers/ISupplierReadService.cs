using StockFlow.Application.Suppliers.GetSuppliers;

namespace StockFlow.Application.Suppliers
{
    public interface ISupplierReadService
    {
        Task<GetSuppliersResult> GetSuppliersAsync(
            int pageNumber,
            int pageSize,
            string? search,
            bool? isActive,
            CancellationToken cancellationToken = default);

        Task<SupplierDto?> GetSupplierByIdAsync(
            Guid supplierId,
            CancellationToken cancellationToken = default); 
    }
}
