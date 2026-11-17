using StockFlow.Domain.Enums;

namespace StockFlow.Application.PurchaseOrders.GetPurchaseOrders
{
    public sealed record GetPurchaseOrdersQuery(
        int PageNumber = 1,
        int PageSize = 20,
        PurchaseOrderStatus? Status = null,
        string? Search = null);
}
