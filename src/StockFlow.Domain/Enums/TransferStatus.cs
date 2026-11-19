namespace StockFlow.Domain.Enums
{
    public enum TransferStatus
    {
        Draft = 1,
        Dispatched = 2,
        PartiallyReceived = 3,
        Received = 4,
        Cancelled = 5,
    }
}
