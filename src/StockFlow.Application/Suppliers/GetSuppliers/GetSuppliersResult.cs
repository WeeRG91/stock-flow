namespace StockFlow.Application.Suppliers.GetSuppliers
{
    public sealed record GetSuppliersResult(
        IReadOnlyList<SupplierDto> Items,
        int PageNumber,
        int PageSize,
        int TotalCount);
}
