namespace StockFlow.Application.PurchaseOrders.ApprovePurchaseOrder
{
    public sealed record ApprovePurchaseOrderCommand(
        Guid PurchaseOrderId,
        Guid ExpectedVersion);
}
