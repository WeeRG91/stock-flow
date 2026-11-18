using StockFlow.Application.Abstractions;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Abstractions.Time;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;

namespace StockFlow.Application.PurchasingNotifications.MarkPurchasingNotificationAsRead
{
    public sealed class MarkPurchasingNotificationAsReadCommandHandler
    {
        private readonly ICurrentUser _currentUser;
        private readonly IPurchasingNotificationRepository _notifications;
        private readonly IClock _clock;

        public MarkPurchasingNotificationAsReadCommandHandler(
            ICurrentUser currentUser,
            IPurchasingNotificationRepository notifications,
            IClock clock)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(notifications);
            ArgumentNullException.ThrowIfNull(clock);

            _currentUser = currentUser;
            _notifications = notifications;
            _clock = clock;
        }

        public async Task HandleAsync(
            MarkPurchasingNotificationAsReadCommand command,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            cancellationToken.ThrowIfCancellationRequested();

            var userId = _currentUser.UserId;
            if (userId is null || userId == Guid.Empty)
            {
                throw new AuthenticationRequiredException();
            }

            if (!await _currentUser.HasPermissionAsync(
                StockFlowPolicies.PurchaseOrdersRead, cancellationToken))
            {
                throw new PermissionDeniedException();
            }

            if (command.NotificationId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.NotificationId), "Notification ID must not be empty.");
            }

            var notification = await _notifications.GetForRecipientAsync(
                command.NotificationId, userId.Value, cancellationToken)
                ?? throw new ResourceNotFoundException(
                    "Purchasing notification", command.NotificationId);

            if (notification.ReadAt is not null)
            {
                return;
            }

            notification.MarkAsRead(_clock.UtcNow);
            await _notifications.SaveAsync(notification, cancellationToken);
        }
    }
}
