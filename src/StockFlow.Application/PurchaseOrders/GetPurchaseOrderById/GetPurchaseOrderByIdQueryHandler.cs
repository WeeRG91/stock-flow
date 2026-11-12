using StockFlow.Application.Abstractions;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;

namespace StockFlow.Application.PurchaseOrders.GetPurchaseOrderById
{
    public sealed class GetPurchaseOrderByIdQueryHandler
    {
        private readonly ICurrentUser _currentUser;
        private readonly IPurchaseOrderReadService _orders;

        public GetPurchaseOrderByIdQueryHandler(
            ICurrentUser currentUser,
            IPurchaseOrderReadService orders)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(orders);

            _currentUser = currentUser;
            _orders = orders;
        }

        public async Task<PurchaseOrderDetailsDto> HandleAsync(
            GetPurchaseOrderByIdQuery query,
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

            if (query.PurchaseOrderId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(query.PurchaseOrderId),
                    "Purchase order ID must not be empty.");
            }

            return await _orders.GetByIdAsync(
                query.PurchaseOrderId, cancellationToken)
                ?? throw new ResourceNotFoundException(
                    "Purchase order", query.PurchaseOrderId);
        }
    }
}
