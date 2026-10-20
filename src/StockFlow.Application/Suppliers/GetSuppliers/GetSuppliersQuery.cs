namespace StockFlow.Application.Suppliers.GetSuppliers
{
    public sealed record GetSuppliersQuery(
        int PageNumber = 1,
        int PageSize = 20,
        string? Search = null,
        bool? IsActive = null);
}
