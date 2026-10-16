namespace StockFlow.Application.Products.GetProducts
{
    public sealed record ProductDto(
        Guid Id,
        string Name,
        string Sku,
        string? Description,
        bool IsActive);
}
