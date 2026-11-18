using StockFlow.Application.PurchasingNotifications.GetPurchasingNotifications;

namespace StockFlow.Application.PurchasingNotifications
{
    public interface IPurchasingNotificationReadService
    {
        Task<GetPurchasingNotificationsResult> GetPageAsync(
            Guid receiptUserId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);
    }
}
