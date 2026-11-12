namespace StockFlow.Application.PurchaseOrders.UpdateDraftPurchaseOrder
{
    public sealed record UpdateDraftPurchaseOrderCommand(
        Guid PurchaseOrderId,
        Guid ExpectedVersion,
        IReadOnlyList<UpdateDraftPurchaseOrderLineCommand> Lines);
}
