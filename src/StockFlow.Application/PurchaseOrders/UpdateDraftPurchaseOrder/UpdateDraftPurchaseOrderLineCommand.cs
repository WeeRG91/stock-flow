namespace StockFlow.Application.PurchaseOrders.UpdateDraftPurchaseOrder
{
    public sealed record UpdateDraftPurchaseOrderLineCommand(
        Guid ProductId,
        int OrderedQuantity,
        decimal UnitPrice);
}
