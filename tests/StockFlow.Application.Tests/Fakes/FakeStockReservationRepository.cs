using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Domain.Entities;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Tests.Fakes
{
    public sealed class FakeStockReservationRepository
        : IStockReservationRepository
    {
        private readonly Dictionary<Guid, StockReservation> _byRequest = new();

        public Task SaveFulfillmentAsync(
            StockReservation reservation,
            InventoryEntity inventory,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FulfillmentSaveCallCount++;
            ReceivedToken = cancellationToken;
            if (SaveException is not null)
            {
                return Task.FromException(SaveException);
            }
            _byId[reservation.Id] = (reservation, Guid.NewGuid());
            LastSavedReservation = reservation;
            LastSavedInventory = inventory;
            return Task.CompletedTask;
        }
        private readonly Dictionary<Guid, (StockReservation Reservation, Guid Version)> _byId = new();

        public int LookupByIdCallCount { get; private set; }
        public int ReleaseSaveCallCount { get; private set; }
        public int FulfillmentSaveCallCount { get; private set; }
        public Guid? ReceivedReservationId { get; private set; }

        public void AddTracked(StockReservation reservation, Guid version)
        {
            _byId.Add(reservation.Id, (reservation, version));
        }

        public Task<(StockReservation Reservation, Guid Version)?> GetByIdAsync(
            Guid reservationId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LookupByIdCallCount++;
            ReceivedReservationId = reservationId;
            ReceivedToken = cancellationToken;
            return Task.FromResult<(StockReservation Reservation, Guid Version)?>(
                _byId.TryGetValue(reservationId, out var loaded) ? loaded : null);
        }

        public Task SaveReleaseAsync(
            StockReservation reservation,
            InventoryEntity inventory,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReleaseSaveCallCount++;
            ReceivedToken = cancellationToken;
            if (SaveException is not null)
            {
                return Task.FromException(SaveException);
            }
            _byId[reservation.Id] = (reservation, Guid.NewGuid());
            LastSavedReservation = reservation;
            LastSavedInventory = inventory;
            return Task.CompletedTask;
        }

        public int GetCallCount { get; private set; }
        public int SaveCallCount { get; private set; }

        public Guid? ReceivedRequestId { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }

        public StockReservation? LastSavedReservation { get; private set; }
        public InventoryEntity? LastSavedInventory { get; private set; }

        public Exception? SaveException { get; set; }

        public void Add(Guid requestId, StockReservation reservation)
        {
            _byRequest.Add(requestId, reservation);
        }

        public Task<StockReservation?> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetCallCount++;
            ReceivedRequestId = requestId;
            ReceivedToken = cancellationToken;

            _byRequest.TryGetValue(requestId, out var reservation);

            return Task.FromResult(reservation);
        }

        public Task SaveCreationAsync(
            StockReservation reservation,
            InventoryEntity inventory,
            Guid requestId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCallCount++;
            ReceivedRequestId = requestId;
            ReceivedToken = cancellationToken;

            if (SaveException is not null)
            {
                return Task.FromException(SaveException);
            }

            _byRequest.Add(requestId, reservation);
            LastSavedReservation = reservation;
            LastSavedInventory = inventory;

            return Task.CompletedTask;
        }
    }
}
