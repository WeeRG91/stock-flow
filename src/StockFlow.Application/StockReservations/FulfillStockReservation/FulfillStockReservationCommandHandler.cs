using StockFlow.Application.Abstractions;
using StockFlow.Application.Abstractions.Notifications;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Abstractions.Time;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.Application.StockReservations.FulfillStockReservation
{
    public sealed class FulfillStockReservationCommandHandler
    {
        private readonly ICurrentUser _currentUser;
        private readonly IStockReservationRepository _reservations;
        private readonly IInventoryRepository _inventories;
        private readonly IClock _clock;
        private readonly IInventoryChangeNotifier _notifier;

        public FulfillStockReservationCommandHandler(
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
            FulfillStockReservationCommand command,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            var userId = await RequireFulfillingUserAsync(cancellationToken);
            ValidateCommand(command);

            var reservation = await LoadFulfillableReservationAsync(
                command,
                cancellationToken);

            var inventory = await _inventories.GetByIdAsync(
                reservation.InventoryId,
                cancellationToken)
                ?? throw new ResourceNotFoundException(
                    "Inventory",
                    reservation.InventoryId);

            if (inventory.QuantityReserved < reservation.Quantity ||
                inventory.QuantityOnHand < reservation.Quantity)
            {
                throw new StockReservationConflictException(
                    reservation.Id,
                    "The inventory balance is inconsistent with this reservation.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var fulfilledAt = _clock.UtcNow;

            reservation.Fulfill(fulfilledAt, userId);

            inventory.ConsumeReservedStock(
                reservation.Quantity,
                $"Stock reservation {reservation.Id} fulfilled",
                fulfilledAt);

            await _reservations.SaveFulfillmentAsync(
                reservation,
                inventory,
                cancellationToken);

            await _notifier.NotifyAsync(
                new InventoryChanged(),
                cancellationToken);
        }

        private async Task<Guid> RequireFulfillingUserAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var userId = _currentUser.UserId;

            if (userId is null || userId == Guid.Empty)
            {
                throw new AuthenticationRequiredException();
            }

            if (!await _currentUser.HasPermissionAsync(
                StockFlowPolicies.StockReservationsFulfill,
                cancellationToken))
            {
                throw new PermissionDeniedException();
            }

            return userId.Value;
        }

        private static void ValidateCommand(
            FulfillStockReservationCommand command)
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

        private async Task<StockReservation> LoadFulfillableReservationAsync(
            FulfillStockReservationCommand command,
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
                    "The reservation has changed. Reload it before fulfilling.");
            }

            if (reservation.Status != StockReservationStatus.Active)
            {
                throw new StockReservationConflictException(
                    reservation.Id,
                    "Only active reservations can be fulfilled.");
            }

            return reservation;
        }
    }
}
