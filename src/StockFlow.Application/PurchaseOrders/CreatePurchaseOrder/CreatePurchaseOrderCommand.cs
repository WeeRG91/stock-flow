namespace StockFlow.Application.PurchaseOrders.CreatePurchaseOrder
{
    public sealed record CreatePurchaseOrderCommand(
        Guid SupplierId,
        Guid WarehouseId,
        IReadOnlyList<CreatePurchaseOrderLineCommand> Lines);
}
