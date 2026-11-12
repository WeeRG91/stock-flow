namespace StockFlow.Domain.Entities
{
    public sealed record PurchaseOrderLineDraft(
        Guid ProductId,
        string ProductSku,
        string ProductName,
        int OrderedQuantity,
        decimal UnitPrice);
}
