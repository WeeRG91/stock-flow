using StockFlow.Application.Abstractions;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;

namespace StockFlow.Application.PurchasingNotifications.GetPurchasingNotifications
{
    public sealed class GetPurchasingNotificationsQueryHandler
    {
        private readonly ICurrentUser _currentUser;
        private readonly IPurchasingNotificationReadService _notifications;

        public GetPurchasingNotificationsQueryHandler(
            ICurrentUser currentUser,
            IPurchasingNotificationReadService notifications)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(notifications);

            _currentUser = currentUser;
            _notifications = notifications;
        }

        public async Task<GetPurchasingNotificationsResult> HandleAsync(
            GetPurchasingNotificationsQuery query,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
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

            if (query.PageNumber < 1)
            {
                throw new RequestValidationException(
                    nameof(query.PageNumber), "Page number must be at least 1.");
            }

            if (query.PageSize is < 1 or > 100)
            {
                throw new RequestValidationException(
                    nameof(query.PageSize), "Page size must be between 1 and 100.");
            }

            var offset = ((long)query.PageNumber - 1) * query.PageSize;
            if (offset > int.MaxValue)
            {
                throw new RequestValidationException(
                    nameof(query.PageNumber), "The requested page is beyond the supported range.");
            }

            return await _notifications.GetPageAsync(
                userId.Value, query.PageNumber, query.PageSize, cancellationToken);
        }
    }
}
