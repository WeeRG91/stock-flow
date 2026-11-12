namespace StockFlow.Application.PurchaseOrders.CreatePurchaseOrder
{
    public sealed record CreatePurchaseOrderLineCommand(
        Guid ProductId,
        int OrderedQuantity,
        decimal UnitPrice);
}
