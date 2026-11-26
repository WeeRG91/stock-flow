using StockFlow.Application.Abstractions;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;

namespace StockFlow.Application.StockReservations.GetStockReservations
{
    public sealed class GetStockReservationsQueryHandler
    {
        private readonly ICurrentUser _currentUser;
        private readonly IStockReservationReadService _reservations;

        public GetStockReservationsQueryHandler(
            ICurrentUser currentUser,
            IStockReservationReadService reservations)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(reservations);

            _currentUser = currentUser;
            _reservations = reservations;
        }

        public async Task<GetStockReservationsResult> HandleAsync(
            GetStockReservationsQuery query,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            await RequireReadingUserAsync(cancellationToken);
            ValidateQuery(query);

            var search = string.IsNullOrWhiteSpace(query.Search)
                ? null
                : query.Search.Trim();

            return await _reservations.GetPageAsync(
                query.PageNumber,
                query.PageSize,
                query.Status,
                search,
                query.InventoryId,
                cancellationToken);
        }

        private async Task RequireReadingUserAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var userId = _currentUser.UserId;

            if (userId is null || userId == Guid.Empty)
            {
                throw new AuthenticationRequiredException();
            }

            if (!await _currentUser.HasPermissionAsync(
                StockFlowPolicies.InventoryRead,
                cancellationToken))
            {
                throw new PermissionDeniedException();
            }
        }

        private static void ValidateQuery(GetStockReservationsQuery query)
        {
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
                    "Select a valid reservation status.");
            }

            if (query.InventoryId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(query.InventoryId),
                    "Inventory ID must not be empty.");
            }

            var offset = ((long)query.PageNumber - 1) * query.PageSize;

            if (offset > int.MaxValue)
            {
                throw new RequestValidationException(
                    nameof(query.PageNumber),
                    "The requested page is beyond the supported range.");
            }
        }
    }
}
