namespace StockFlow.Application.PurchasingNotifications.GetPurchasingNotifications
{
    public sealed record GetPurchasingNotificationsQuery(
        int PageNumber = 1,
        int PageSize = 20);
}
