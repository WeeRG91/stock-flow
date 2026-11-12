namespace StockFlow.Application.PurchaseOrders.SubmitPurchaseOrder
{
    public sealed record SubmitPurchaseOrderCommand(
        Guid PurchaseOrderId,
        Guid ExpectedVersion);
}
