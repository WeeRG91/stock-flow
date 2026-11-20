namespace StockFlow.Domain.Entities
{
    public sealed record TransferLineDraft(
        Guid ProductId,
        string ProductSku,
        string ProductName,
        int Quantity);
}
