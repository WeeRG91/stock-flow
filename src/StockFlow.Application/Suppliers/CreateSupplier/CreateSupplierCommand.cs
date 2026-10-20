namespace StockFlow.Application.Suppliers.CreateSupplier
{
    public sealed record CreateSupplierCommand(
        string Code,
        string Name,
        string ContactEmail,
        string? PhoneNumber = null);
}
