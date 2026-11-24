using StockFlow.Application.Abstractions;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;

namespace StockFlow.Application.Transfers.GetTransfers
{
    public sealed class GetTransfersQueryHandler
    {
        private readonly ICurrentUser _currentUser;
        private readonly ITransferReadService _transfers;

        public GetTransfersQueryHandler(
        ICurrentUser currentUser,
        ITransferReadService transfers)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(transfers);

            _currentUser = currentUser;
            _transfers = transfers;
        }

        public async Task<GetTransfersResult> HandleAsync(
            GetTransfersQuery query,
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

            if (query.Status is { } status && !Enum.IsDefined(status))
            {
                throw new RequestValidationException(
                    nameof(query.Status),
                    "Select a valid transfer status.");
            }

            var offset = ((long)query.PageNumber - 1) * query.PageSize;

            if (offset > int.MaxValue)
            {
                throw new RequestValidationException(
                    nameof(query.PageNumber),
                    "The requested page is beyond the supported range.");
            }

            var search = string.IsNullOrWhiteSpace(query.Search)
                ? null
                : query.Search.Trim();

            return await _transfers.GetPageAsync(
                query.PageNumber,
                query.PageSize,
                query.Status,
                search,
                cancellationToken);
        }
    }
}
