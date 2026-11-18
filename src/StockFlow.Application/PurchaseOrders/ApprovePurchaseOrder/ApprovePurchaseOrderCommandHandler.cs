using StockFlow.Application.Abstractions;
using StockFlow.Application.Abstractions.Notifications;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Abstractions.Time;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.Application.PurchaseOrders.ApprovePurchaseOrder
{
    public sealed class ApprovePurchaseOrderCommandHandler
    {
        private readonly ICurrentUser _currentUser;
        private readonly IPurchaseOrderRepository _orders;
        private readonly IClock _clock;
        private readonly IPurchasingChangeNotifier _notifier;

        public ApprovePurchaseOrderCommandHandler(
            ICurrentUser currentUser,
            IPurchaseOrderRepository orders,
            IClock clock,
            IPurchasingChangeNotifier notifier)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(orders);
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(notifier);

            _currentUser = currentUser;
            _orders = orders;
            _clock = clock;
            _notifier = notifier;
        }

        public async Task HandleAsync(
            ApprovePurchaseOrderCommand command,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            cancellationToken.ThrowIfCancellationRequested();

            if (_currentUser.UserId is not Guid actorId || actorId == Guid.Empty)
            {
                throw new AuthenticationRequiredException();
            }

            if (!await _currentUser.HasPermissionAsync(
                StockFlowPolicies.PurchaseOrdersApprove, cancellationToken))
            {
                throw new PermissionDeniedException();
            }

            if (command.PurchaseOrderId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.PurchaseOrderId),
                    "Purchase order ID must not be empty.");
            }

            if (command.ExpectedVersion == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.ExpectedVersion),
                    "Expected version must not be empty.");
            }

            var (order, version) = await _orders.GetByIdAsync(
                command.PurchaseOrderId, cancellationToken)
                ?? throw new ResourceNotFoundException(
                    "Purchase order", command.PurchaseOrderId);

            if (order.CreatedByUserId == actorId)
            {
                throw new PermissionDeniedException();
            }

            if (version != command.ExpectedVersion)
            {
                throw new PurchaseOrderConflictException(
                    order.Id,
                    "The purchase order has changed. Reload it and try again.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var approvedAt = _clock.UtcNow;

            try
            {
                order.Approve(approvedAt, actorId);
            }
            catch (InvalidOperationException exception)
            {
                throw new BusinessRuleException(exception.Message);
            }

            var notification = new PurchasingNotification(
                order.CreatedByUserId,
                order.Id,
                PurchasingNotificationKind.PurchaseOrderApproved,
                approvedAt);

            await _orders.SaveAsync(order, cancellationToken, notifications: [notification]);

            await _notifier.NotifyAsync(
                [notification.RecipientUserId], cancellationToken);
        }
    }
}
