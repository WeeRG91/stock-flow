using StockFlow.Domain.Enums;

namespace StockFlow.Domain.Entities
{
    public class StockMovement
    {
        public Guid Id { get; }
        public Guid InventoryId { get; }
        public int QuantityChange { get; }
        public StockMovementType Type { get; }
        public string Reason { get; }
        public DateTimeOffset OccurredAt { get; }

        public StockMovement(
            Guid inventoryId,
            int quantityChange,
            StockMovementType type,
            string reason,
            DateTimeOffset occurredAt)
        {
            if (inventoryId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Inventory ID must not be empty.",
                    nameof(inventoryId));
            }

            if (!Enum.IsDefined(type))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(type),
                    "Movement type must be a defined value.");
            }

            if (quantityChange == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantityChange),
                    "Quantity change must not be zero.");
            }

            if (type == StockMovementType.Receipt && quantityChange < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantityChange),
                    "Receipt quantity must be positive.");
            }

            if (type == StockMovementType.Removal && quantityChange > 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantityChange),
                    "Removal quantity must be negative.");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException(
                "Reason must not be empty or whitespace.",
                nameof(reason));
            }

            if (occurredAt == default(DateTimeOffset))
            {
                throw new ArgumentOutOfRangeException(
                   nameof(occurredAt),
                   "Occurrence timestamp must not be the default value.");
            }

            Id = Guid.NewGuid();
            InventoryId = inventoryId;
            QuantityChange = quantityChange;
            Type = type;
            Reason = reason;
            OccurredAt = occurredAt;
        }
    }
}
