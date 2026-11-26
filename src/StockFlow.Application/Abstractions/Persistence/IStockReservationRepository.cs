using StockFlow.Domain.Entities;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Abstractions.Persistence
{
    public interface IStockReservationRepository
    {
        Task<StockReservation?> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically persists the new reservation, its creation request ID,
        /// and the updated reserved balance on an existing tracked inventory.
        /// </summary>
        Task SaveCreationAsync(
            StockReservation reservation,
            InventoryEntity inventory,
            Guid requestId,
            CancellationToken cancellationToken = default);

        Task<(StockReservation Reservation, Guid Version)?> GetByIdAsync(
            Guid reservationId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically persists a tracked Active-to-Released reservation
        /// and the corresponding decrease in Inventory's reserved balance.
        /// </summary>
        Task SaveReleaseAsync(
            StockReservation reservation,
            InventoryEntity inventory,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically persists a tracked Active-to-Fulfilled reservation,
        /// the corresponding Inventory balance changes, and its Removal movement.
        /// Acknowledges the persisted movement only after a successful save.
        /// </summary>
        Task SaveFulfillmentAsync(
            StockReservation reservation,
            InventoryEntity inventory,
            CancellationToken cancellationToken = default);
    }
}
