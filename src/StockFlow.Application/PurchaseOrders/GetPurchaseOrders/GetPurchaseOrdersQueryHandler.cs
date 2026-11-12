using StockFlow.Application.Abstractions;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;

namespace StockFlow.Application.PurchaseOrders.GetPurchaseOrders
{
    public sealed class GetPurchaseOrdersQueryHandler
    {
        private readonly ICurrentUser _currentUser;
        private readonly IPurchaseOrderReadService _orders;

        public GetPurchaseOrdersQueryHandler(
            ICurrentUser currentUser,
            IPurchaseOrderReadService orders)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(orders);

            _currentUser = currentUser;
            _orders = orders;
        }

        public async Task<GetPurchaseOrdersResult> HandleAsync(
            GetPurchaseOrdersQuery query,
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
                    nameof(query.PageNumber),
                    "Page number must be at least 1.");
            }

            if (query.PageSize is < 1 or > 100)
            {
                throw new RequestValidationException(
                    nameof(query.PageSize),
                    "Page size must be between 1 and 100.");
            }

            var offset = ((long)query.PageNumber - 1) * query.PageSize;

            if (offset > int.MaxValue)
            {
                throw new RequestValidationException(
                    nameof(query.PageNumber),
                    "The requested page is beyond the supported range.");
            }

            return await _orders.GetPageAsync(
                query.PageNumber, query.PageSize, cancellationToken);
        }
    }
}
