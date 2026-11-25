using StockFlow.Domain.Enums;

namespace StockFlow.Domain.Entities
{
    public sealed class StockReservation
    {
        public Guid Id { get; }
        public Guid InventoryId { get; }
        public int Quantity { get; }
        public string Reference { get; }
        public DateTimeOffset CreatedAt { get; }
        public Guid CreatedByUserId { get; }

        public StockReservationStatus Status { get; private set; }

        public DateTimeOffset? ReleasedAt { get; private set; }
        public Guid? ReleasedByUserId { get; private set; }

        public DateTimeOffset? FulfilledAt { get; private set; }
        public Guid? FulfilledByUserId { get; private set; }

        public StockReservation(
            Guid inventoryId,
            int quantity,
            string reference,
            DateTimeOffset createdAt,
            Guid createdByUserId)
        {
            if (inventoryId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Inventory ID must not be empty.",
                    nameof(inventoryId));
            }

            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    "Quantity must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(reference))
            {
                throw new ArgumentException(
                    "Reference must not be empty or whitespace.",
                    nameof(reference));
            }

            if (createdAt == default || createdAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Creation time must be a non-default UTC value.",
                    nameof(createdAt));
            }

            if (createdByUserId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Creator ID must not be empty.",
                    nameof(createdByUserId));
            }

            Id = Guid.NewGuid();
            InventoryId = inventoryId;
            Quantity = quantity;
            Reference = reference.Trim();
            CreatedAt = createdAt;
            CreatedByUserId = createdByUserId;
            Status = StockReservationStatus.Active;
        }

        public void Release(
            DateTimeOffset releasedAt,
            Guid releasedByUserId)
        {
            if (Status != StockReservationStatus.Active)
            {
                throw new InvalidOperationException(
                    "Only active reservations can be released.");
            }

            if (releasedAt == default || releasedAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Release time must be a non-default UTC value.",
                    nameof(releasedAt));
            }

            if (releasedAt < CreatedAt)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(releasedAt),
                    "Release time cannot precede creation.");
            }

            if (releasedByUserId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Releasing user ID must not be empty.",
                    nameof(releasedByUserId));
            }

            ReleasedAt = releasedAt;
            ReleasedByUserId = releasedByUserId;
            Status = StockReservationStatus.Released;
        }

        public void Fulfill(
            DateTimeOffset fulfilledAt,
            Guid fulfilledByUserId)
        {
            if (Status != StockReservationStatus.Active)
            {
                throw new InvalidOperationException(
                    "Only active reservations can be fulfilled.");
            }

            if (fulfilledAt == default || fulfilledAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Fulfillment time must be a non-default UTC value.",
                    nameof(fulfilledAt));
            }

            if (fulfilledAt < CreatedAt)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fulfilledAt),
                    "Fulfillment time cannot precede creation.");
            }

            if (fulfilledByUserId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Fulfilling user ID must not be empty.",
                    nameof(fulfilledByUserId));
            }

            FulfilledAt = fulfilledAt;
            FulfilledByUserId = fulfilledByUserId;
            Status = StockReservationStatus.Fulfilled;
        }
    }
}
