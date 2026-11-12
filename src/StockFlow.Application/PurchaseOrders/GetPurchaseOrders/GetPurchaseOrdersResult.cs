namespace StockFlow.Application.PurchaseOrders.GetPurchaseOrders
{
    public sealed record GetPurchaseOrdersResult(
        IReadOnlyList<PurchaseOrderSummaryDto> Items,
        int PageNumber,
        int PageSize,
        int TotalCount);
}
