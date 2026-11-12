namespace StockFlow.Application.PurchaseOrders.GetPurchaseOrders
{
    public sealed record GetPurchaseOrdersQuery(
        int PageNumber = 1,
        int PageSize = 20);
}
