using StockFlow.Application.Abstractions;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;

namespace StockFlow.Application.Transfers.GetTransferById
{
    public sealed class GetTransferByIdQueryHandler
    {
        private readonly ICurrentUser _currentUser;
        private readonly ITransferReadService _transfers;

        public GetTransferByIdQueryHandler(
            ICurrentUser currentUser,
            ITransferReadService transfers)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(transfers);

            _currentUser = currentUser;
            _transfers = transfers;
        }

        public async Task<TransferDetailsDto> HandleAsync(
            GetTransferByIdQuery query,
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
                StockFlowPolicies.TransfersRead, cancellationToken))
            {
                throw new PermissionDeniedException();
            }

            if (query.TransferId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(query.TransferId),
                    "Transfer ID must not be empty.");
            }

            return await _transfers.GetByIdAsync(
                query.TransferId, cancellationToken)
                ?? throw new ResourceNotFoundException(
                    "Transfer", query.TransferId);
        }
    }
}
