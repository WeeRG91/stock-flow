using StockFlow.Application.Abstractions;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Abstractions.Time;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;

namespace StockFlow.Application.PurchaseOrders.SubmitPurchaseOrder
{
    public sealed class SubmitPurchaseOrderCommandHandler
    {
        private readonly ICurrentUser _currentUser;
        private readonly IPurchaseOrderRepository _orders;
        private readonly IClock _clock;

        public SubmitPurchaseOrderCommandHandler(
            ICurrentUser currentUser,
            IPurchaseOrderRepository orders,
            IClock clock)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(orders);
            ArgumentNullException.ThrowIfNull(clock);

            _currentUser = currentUser;
            _orders = orders;
            _clock = clock;
        }

        public async Task HandleAsync(
            SubmitPurchaseOrderCommand command,
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
                StockFlowPolicies.PurchaseOrdersManage, cancellationToken))
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

            if (version != command.ExpectedVersion)
            {
                throw new PurchaseOrderConflictException(
                    order.Id,
                    "The purchase order has changed. Reload it and try again.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var submittedAt = _clock.UtcNow;

            try
            {
                order.Submit(submittedAt);
            }
            catch (InvalidOperationException exception)
            {
                throw new BusinessRuleException(exception.Message);
            }

            await _orders.SaveAsync(order, cancellationToken);
        }
    }
}
