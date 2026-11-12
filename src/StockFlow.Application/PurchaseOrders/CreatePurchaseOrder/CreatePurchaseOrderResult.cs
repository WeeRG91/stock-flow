using StockFlow.Domain.Enums;

namespace StockFlow.Application.PurchaseOrders.CreatePurchaseOrder
{
    public sealed record CreatePurchaseOrderResult(
        Guid PurchaseOrderId,
        string OrderNumber,
        PurchaseOrderStatus Status,
        string Currency,
        decimal TotalAmount,
        DateTimeOffset CreatedAt,
        Guid CreatedByUserId);
}
