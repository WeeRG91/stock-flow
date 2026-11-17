using StockFlow.Application.Abstractions;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;

namespace StockFlow.Application.GoodsReceipts.GetGoodsReceipts
{
    public sealed class GetGoodsReceiptQueryHandler
    {
        private readonly ICurrentUser _currentUser;
        private readonly IGoodsReceiptReadService _receipts;

        public GetGoodsReceiptQueryHandler(
            ICurrentUser currentUser,
            IGoodsReceiptReadService receipts)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(receipts);

            _currentUser = currentUser;
            _receipts = receipts;
        }

        public async Task<IReadOnlyList<GoodsReceiptHistoryItemDto>> HandleAsync(
            GetGoodsReceiptQuery query,
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

            return await _receipts.GetByPurchaseOrderIdAsync(
                query.PurchaseOrderId, cancellationToken)
                ?? throw new ResourceNotFoundException(
                    "Purchase order", query.PurchaseOrderId);
        }
    }
}
