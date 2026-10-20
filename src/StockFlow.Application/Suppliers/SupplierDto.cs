namespace StockFlow.Application.Suppliers
{
    public sealed record SupplierDto(
        Guid Id,
        string Code,
        string Name,
        string ContactEmail,
        string? PhoneNumber,
        bool IsActive);
}
