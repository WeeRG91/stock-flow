using StockFlow.Domain.Entities;

namespace StockFlow.Application.Abstractions.Persistence
{
    public interface IPurchasingNotificationRepository
    {
        Task<PurchasingNotification?> GetForRecipientAsync(
            Guid notification,
            Guid recipientUserId,
            CancellationToken cancellationToken = default);

        Task SaveAsync(
            PurchasingNotification notification,
            CancellationToken cancellationToken = default);
    }
}
