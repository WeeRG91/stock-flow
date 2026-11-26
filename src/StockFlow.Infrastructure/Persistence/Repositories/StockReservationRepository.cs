using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Persistence.Repositories
{
    public sealed class StockReservationRepository : IStockReservationRepository
    {
        private readonly StockFlowDbContext _context;
        private bool _saveFailed;

        public StockReservationRepository(StockFlowDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        public Task<StockReservation?> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_saveFailed)
            {
                throw new InvalidOperationException(
                    "A previous save failed. Discard this scope and its DbContext.");
            }

            return _context.StockReservations
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    reservation =>
                        EF.Property<Guid>(reservation, "RequestId") == requestId,
                    cancellationToken);
        }

        public async Task SaveCreationAsync(
            StockReservation reservation,
            InventoryEntity inventory,
            Guid requestId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reservation);
            ArgumentNullException.ThrowIfNull(inventory);
            cancellationToken.ThrowIfCancellationRequested();

            if (_saveFailed)
            {
                throw new InvalidOperationException(
                    "A previous save failed. Discard this scope and its DbContext.");
            }

            if (requestId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Request ID must not be empty.",
                    nameof(requestId));
            }

            if (_context.Database.CurrentTransaction is not null ||
                System.Transactions.Transaction.Current is not null)
            {
                throw new InvalidOperationException(
                    "Reservation creation must own its commit boundary.");
            }

            if (reservation.InventoryId != inventory.Id ||
                reservation.Status != StockReservationStatus.Active)
            {
                throw new InvalidOperationException(
                    "Creation requires an active reservation for this inventory.");
            }

            if (_context.Entry(reservation).State != EntityState.Detached)
            {
                throw new InvalidOperationException(
                    "Creation requires a new, untracked reservation.");
            }

            var inventoryEntry = _context.Entry(inventory);

            if (inventoryEntry.State is not
                (EntityState.Unchanged or EntityState.Modified))
            {
                throw new InvalidOperationException(
                    "Creation requires an existing inventory tracked by this DbContext.");
            }

            var originalReserved = inventoryEntry
                .Property(item => item.QuantityReserved).OriginalValue;

            var originalOnHand = inventoryEntry
                .Property(item => item.QuantityOnHand).OriginalValue;

            if (inventory.QuantityReserved != (long)originalReserved + reservation.Quantity ||
                inventory.QuantityOnHand != originalOnHand ||
                inventory.PendingMovements.Count != 0)
            {
                throw new InvalidOperationException(
                    "Creation must increase reserved stock by the reservation quantity " +
                    "without changing physical stock or creating movements.");
            }

            try
            {
                var reservationEntry = _context.StockReservations.Add(reservation);

                reservationEntry.Property<Guid>("RequestId").CurrentValue = requestId;
                reservationEntry.Property<Guid>("Version").CurrentValue = Guid.NewGuid();

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _saveFailed = true;

                throw new StockReservationConflictException(
                    reservation.Id,
                    "Inventory changed during reservation creation. " +
                    "Reload the current balance before trying again.");
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "IX_StockReservations_RequestId"
                })
            {
                _saveFailed = true;

                throw new StockReservationConflictException(
                    reservation.Id,
                    "Another request already used this creation request ID. " +
                    "Retry the same request in a new operation scope.");
            }
            catch
            {
                _saveFailed = true;
                throw;
            }
        }

        public async Task<(StockReservation Reservation, Guid Version)?> GetByIdAsync(
            Guid reservationId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_saveFailed)
            {
                throw new InvalidOperationException(
                    "A previous save failed. Discard this scope and its DbContext.");
            }

            var reservation = await _context.StockReservations
                .AsTracking()
                .SingleOrDefaultAsync(
                    reservation => reservation.Id == reservationId,
                    cancellationToken);

            if (reservation is null)
            {
                return null;
            }

            var version = _context.Entry(reservation)
                .Property<Guid>("Version")
                .OriginalValue;

            return (reservation, version);
        }

        public async Task SaveReleaseAsync(
            StockReservation reservation,
            InventoryEntity inventory,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reservation);
            ArgumentNullException.ThrowIfNull(inventory);
            cancellationToken.ThrowIfCancellationRequested();

            if (_saveFailed)
            {
                throw new InvalidOperationException(
                    "A previous save failed. Discard this scope and its DbContext.");
            }

            if (_context.Database.CurrentTransaction is not null ||
                System.Transactions.Transaction.Current is not null)
            {
                throw new InvalidOperationException(
                    "Reservation release must own its commit boundary.");
            }

            var reservationEntry = _context.Entry(reservation);

            if (reservationEntry.State is not
                    (EntityState.Unchanged or EntityState.Modified) ||
                reservationEntry.Property(item => item.Status).OriginalValue
                    != StockReservationStatus.Active ||
                reservation.Status != StockReservationStatus.Released)
            {
                throw new InvalidOperationException(
                    "Release requires a tracked Active-to-Released reservation.");
            }

            var inventoryEntry = _context.Entry(inventory);

            if (inventoryEntry.State is not
                (EntityState.Unchanged or EntityState.Modified))
            {
                throw new InvalidOperationException(
                    "Release requires an existing tracked inventory.");
            }

            var originalReserved = inventoryEntry
                .Property(item => item.QuantityReserved).OriginalValue;

            var originalOnHand = inventoryEntry
                .Property(item => item.QuantityOnHand).OriginalValue;

            if (reservation.InventoryId != inventory.Id ||
                inventory.QuantityReserved != (long)originalReserved - reservation.Quantity ||
                inventory.QuantityOnHand != originalOnHand ||
                inventory.PendingMovements.Count != 0)
            {
                throw new InvalidOperationException(
                    "Release must decrease reserved stock by the reservation quantity " +
                    "without changing physical stock or creating movements.");
            }

            try
            {
                reservationEntry.Property<Guid>("Version").CurrentValue =
                    Guid.NewGuid();

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _saveFailed = true;

                throw new StockReservationConflictException(
                    reservation.Id,
                    "The reservation or inventory changed during release. " +
                    "Reload before trying again.");
            }
            catch
            {
                _saveFailed = true;
                throw;
            }
        }

        public async Task SaveFulfillmentAsync(
            StockReservation reservation,
            InventoryEntity inventory,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reservation);
            ArgumentNullException.ThrowIfNull(inventory);
            cancellationToken.ThrowIfCancellationRequested();

            if (_saveFailed)
            {
                throw new InvalidOperationException(
                    "A previous save failed. Discard this scope and its DbContext.");
            }

            if (_context.Database.CurrentTransaction is not null ||
                System.Transactions.Transaction.Current is not null)
            {
                throw new InvalidOperationException(
                    "Reservation fulfillment must own its commit boundary.");
            }

            var reservationEntry = _context.Entry(reservation);

            if (reservationEntry.State is not
                    (EntityState.Unchanged or EntityState.Modified) ||
                reservationEntry.Property(item => item.Status).OriginalValue
                    != StockReservationStatus.Active ||
                reservation.Status != StockReservationStatus.Fulfilled)
            {
                throw new InvalidOperationException(
                    "Fulfillment requires a tracked Active-to-Fulfilled reservation.");
            }

            var inventoryEntry = _context.Entry(inventory);

            if (inventoryEntry.State is not
                (EntityState.Unchanged or EntityState.Modified))
            {
                throw new InvalidOperationException(
                    "Fulfillment requires an existing tracked inventory.");
            }

            var originalReserved = inventoryEntry
                .Property(item => item.QuantityReserved).OriginalValue;

            var originalOnHand = inventoryEntry
                .Property(item => item.QuantityOnHand).OriginalValue;

            if (reservation.InventoryId != inventory.Id ||
                inventory.QuantityReserved != (long)originalReserved - reservation.Quantity ||
                inventory.QuantityOnHand != (long)originalOnHand - reservation.Quantity)
            {
                throw new InvalidOperationException(
                    "Fulfillment must decrease on-hand and reserved stock " +
                    "by the reservation quantity.");
            }

            var movements = inventory.PendingMovements.ToArray();

            if (movements.Length != 1)
            {
                throw new InvalidOperationException(
                    "Fulfillment requires exactly one pending stock movement.");
            }

            var movement = movements[0];

            if (movement.InventoryId != inventory.Id ||
                movement.Type != StockMovementType.Removal ||
                movement.QuantityChange != -reservation.Quantity ||
                movement.OccurredAt != reservation.FulfilledAt)
            {
                throw new InvalidOperationException(
                    "The movement must match the fulfilled reservation's " +
                    "inventory, quantity, and timestamp.");
            }

            if (_context.Entry(movement).State != EntityState.Detached)
            {
                throw new InvalidOperationException(
                    "The fulfillment movement must not already be tracked.");
            }

            try
            {
                reservationEntry.Property<Guid>("Version").CurrentValue =
                    Guid.NewGuid();

                _context.StockMovements.Add(movement);

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _saveFailed = true;

                throw new StockReservationConflictException(
                    reservation.Id,
                    "The reservation or inventory changed during fulfillment. " +
                    "Reload before trying again.");
            }
            catch
            {
                _saveFailed = true;
                throw;
            }

            inventory.AcknowledgePersistedMovements(
                new[] { movement.Id });
        }
    }
}
