using StockFlow.Application.Abstractions;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;

namespace StockFlow.Application.StockReservations.GetStockReservationById
{
    public sealed class GetStockReservationByIdQueryHandler
    {
        private readonly ICurrentUser _currentUser;
        private readonly IStockReservationReadService _reservations;

        public GetStockReservationByIdQueryHandler(
            ICurrentUser currentUser,
            IStockReservationReadService reservations)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(reservations);

            _currentUser = currentUser;
            _reservations = reservations;
        }

        public async Task<StockReservationDto> HandleAsync(
            GetStockReservationByIdQuery query,
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
                StockFlowPolicies.InventoryRead,
                cancellationToken))
            {
                throw new PermissionDeniedException();
            }

            if (query.ReservationId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(query.ReservationId),
                    "Reservation ID must not be empty.");
            }

            return await _reservations.GetByIdAsync(
                query.ReservationId,
                cancellationToken)
                ?? throw new ResourceNotFoundException(
                    "Stock reservation",
                    query.ReservationId);
        }
    }
}
