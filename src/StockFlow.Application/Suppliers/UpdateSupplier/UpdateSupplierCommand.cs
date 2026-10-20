namespace StockFlow.Application.Suppliers.UpdateSupplier
{
    public sealed record UpdateSupplierCommand(
        Guid SupplierId,
        string Name,
        string ContactEmail,
        string? PhoneNumber = null);
}
