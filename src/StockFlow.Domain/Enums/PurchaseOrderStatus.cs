namespace StockFlow.Domain.Enums
{
    public enum PurchaseOrderStatus
    {
        Draft = 1,
        PendingApproval = 2,
        Approved = 3,
        Rejected = 4,
        PartiallyReceived = 5,
        Received = 6,
    }
}
