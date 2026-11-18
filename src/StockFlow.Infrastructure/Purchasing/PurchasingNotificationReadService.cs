using Microsoft.EntityFrameworkCore;
using StockFlow.Application.PurchasingNotifications;
using StockFlow.Application.PurchasingNotifications.GetPurchasingNotifications;
using StockFlow.Infrastructure.Persistence;

namespace StockFlow.Infrastructure.Purchasing
{
    public sealed class PurchasingNotificationReadService : IPurchasingNotificationReadService
    {
        private readonly StockFlowDbContext _context;

        public PurchasingNotificationReadService(StockFlowDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        public async Task<GetPurchasingNotificationsResult> GetPageAsync(
            Guid recipientUserId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var inbox = _context.PurchasingNotifications
                .AsNoTracking()
                .Where(notification => notification.RecipientUserId == recipientUserId);

            var totalCount = await inbox.CountAsync(cancellationToken);
            var unreadCount = await inbox.CountAsync(
                notification => notification.ReadAt == null, cancellationToken);

            var offset = checked((int)(((long)pageNumber - 1) * pageSize));

            var items = await inbox
                .OrderByDescending(notification => notification.CreatedAt)
                .ThenByDescending(notification => notification.Id)
                .Skip(offset)
                .Take(pageSize)
                .Select(notification => new PurchasingNotificationDto(
                    notification.Id,
                    notification.PurchaseOrderId,
                    notification.Kind,
                    notification.CreatedAt,
                    notification.ReadAt))
                .ToListAsync(cancellationToken);

            return new GetPurchasingNotificationsResult(
                items, pageNumber, pageSize, totalCount, unreadCount);
        }
    }
}
