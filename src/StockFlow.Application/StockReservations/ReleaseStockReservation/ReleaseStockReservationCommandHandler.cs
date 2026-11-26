using StockFlow.Application.Abstractions;
using StockFlow.Application.Abstractions.Notifications;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Abstractions.Time;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.Application.StockReservations.ReleaseStockReservation
{
    public sealed class ReleaseStockReservationCommandHandler
    {
        private readonly ICurrentUser _currentUser;
        private readonly IStockReservationRepository _reservations;
        private readonly IInventoryRepository _inventories;
        private readonly IClock _clock;
        private readonly IInventoryChangeNotifier _notifier;

        public ReleaseStockReservationCommandHandler(
            ICurrentUser currentUser,
            IStockReservationRepository reservations,
            IInventoryRepository inventories,
            IClock clock,
            IInventoryChangeNotifier notifier)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(reservations);
            ArgumentNullException.ThrowIfNull(inventories);
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(notifier);

            _currentUser = currentUser;
            _reservations = reservations;
            _inventories = inventories;
            _clock = clock;
            _notifier = notifier;
        }

        public async Task HandleAsync(
            ReleaseStockReservationCommand command,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            var userId = await RequireManagingUserAsync(cancellationToken);
            ValidateCommand(command);

            var reservation = await LoadReleasableReservationAsync(
                command,
                cancellationToken);

            var inventory = await _inventories.GetByIdAsync(
                reservation.InventoryId,
                cancellationToken)
                ?? throw new ResourceNotFoundException(
                    "Inventory",
                    reservation.InventoryId);

            if (inventory.QuantityReserved < reservation.Quantity)
            {
                throw new StockReservationConflictException(
                    reservation.Id,
                    "The reserved balance is inconsistent with this reservation.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            reservation.Release(_clock.UtcNow, userId);
            inventory.ReleaseReservedStock(reservation.Quantity);

            await _reservations.SaveReleaseAsync(
                reservation,
                inventory,
                cancellationToken);

            await _notifier.NotifyAsync(
                new InventoryChanged(),
                cancellationToken);
        }

        private async Task<Guid> RequireManagingUserAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var userId = _currentUser.UserId;

            if (userId is null || userId == Guid.Empty)
            {
                throw new AuthenticationRequiredException();
            }

            if (!await _currentUser.HasPermissionAsync(
                StockFlowPolicies.StockReservationsManage,
                cancellationToken))
            {
                throw new PermissionDeniedException();
            }

            return userId.Value;
        }

        private static void ValidateCommand(
            ReleaseStockReservationCommand command)
        {
            if (command.ReservationId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.ReservationId),
                    "Reservation ID must not be empty.");
            }

            if (command.ExpectedVersion == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.ExpectedVersion),
                    "Expected version must not be empty.");
            }
        }

        private async Task<StockReservation> LoadReleasableReservationAsync(
            ReleaseStockReservationCommand command,
            CancellationToken cancellationToken)
        {
            var loaded = await _reservations.GetByIdAsync(
                command.ReservationId,
                cancellationToken)
                ?? throw new ResourceNotFoundException(
                    "Stock reservation",
                    command.ReservationId);

            var (reservation, version) = loaded;

            if (version != command.ExpectedVersion)
            {
                throw new StockReservationConflictException(
                    reservation.Id,
                    "The reservation has changed. Reload it before releasing.");
            }

            if (reservation.Status != StockReservationStatus.Active)
            {
                throw new StockReservationConflictException(
                    reservation.Id,
                    "Only active reservations can be released.");
            }

            return reservation;
        }
    }
}
