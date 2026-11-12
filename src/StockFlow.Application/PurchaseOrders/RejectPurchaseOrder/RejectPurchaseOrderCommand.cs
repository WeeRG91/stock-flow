namespace StockFlow.Application.PurchaseOrders.RejectPurchaseOrder
{
    public sealed record RejectPurchaseOrderCommand(
        Guid PurchaseOrderId,
        Guid ExpectedVersion);
}
