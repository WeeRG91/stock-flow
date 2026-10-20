namespace StockFlow.Application.Suppliers.CreateSupplier
{
    public sealed record CreateSupplierResult(
        Guid SupplierId,
        string Code,
        string Name,
        string ContactEmail,
        string? PhoneNumber,
        bool IsActive);
}
