namespace StockFlow.Application.Transfers
{
    public sealed record TransferLineDto(
        Guid Id,
        Guid ProductId,
        string ProductSku,
        string ProductName,
        int Quantity,
        int DispatchedQuantity,
        int ReceivedQuantity)
    {
        public int InTransitQuantity => DispatchedQuantity - ReceivedQuantity;
    }
}
