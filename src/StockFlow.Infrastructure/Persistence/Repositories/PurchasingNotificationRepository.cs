using Microsoft.EntityFrameworkCore;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Domain.Entities;

namespace StockFlow.Infrastructure.Persistence.Repositories
{
    public sealed class PurchasingNotificationRepository : IPurchasingNotificationRepository
    {
        private readonly StockFlowDbContext _context;

        public PurchasingNotificationRepository(StockFlowDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        public Task<PurchasingNotification?> GetForRecipientAsync(
            Guid notificationId,
            Guid recipientUserId,
            CancellationToken cancellationToken = default)
        {
            return _context.PurchasingNotifications
                .AsNoTracking()
                .SingleOrDefaultAsync(notification =>
                    notification.Id == notificationId &&
                    notification.RecipientUserId == recipientUserId,
                    cancellationToken);
        }

        public async Task SaveAsync(
            PurchasingNotification notification,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(notification);

            if (notification.ReadAt is null)
            {
                throw new InvalidOperationException(
                    "Mark the notification as read before saving it.");
            }

            var readAt = notification.ReadAt;

            await _context.PurchasingNotifications
                .Where(item =>
                    item.Id == notification.Id &&
                    item.RecipientUserId == notification.RecipientUserId &&
                    item.ReadAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(item => item.ReadAt, readAt),
                    cancellationToken);
        }
    }
}
