namespace StockFlow.Domain.Entities
{
    public sealed class StockCountLine
    {
        public Guid Id { get; }
        public Guid InventoryId { get; }
        public Guid ProductId { get; }
        public string ProductSku { get; }
        public string ProductName { get; }
        public int CapturedQuantityOnHand { get; }
        public int CountedQuantity { get; }

        public int Variance => checked(CountedQuantity - CapturedQuantityOnHand);

        public string CapturedInventoryVersion { get; }
        public DateTimeOffset CountedAt { get; }
        public Guid CountedByUserId { get; }
        public string Notes { get; }

        internal StockCountLine(
            Guid inventoryId,
            Guid productId,
            string productSku,
            string productName,
            int capturedQuantityOnHand,
            int countedQuantity,
            string capturedInventoryVersion,
            DateTimeOffset countedAt,
            Guid countedByUserId,
            string notes)
        {
            if (inventoryId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Inventory ID must not be empty.",
                    nameof(inventoryId));
            }

            if (productId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Product ID must not be empty.",
                    nameof(productId));
            }

            if (string.IsNullOrWhiteSpace(productSku))
            {
                throw new ArgumentException(
                    "Product SKU must not be empty.",
                    nameof(productSku));
            }

            if (string.IsNullOrWhiteSpace(productName))
            {
                throw new ArgumentException(
                    "Product name must not be empty.",
                    nameof(productName));
            }

            if (capturedQuantityOnHand < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(capturedQuantityOnHand),
                    "Captured quantity must not be negative.");
            }

            if (countedQuantity < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(countedQuantity),
                    "Counted quantity must not be negative.");
            }

            if (string.IsNullOrWhiteSpace(capturedInventoryVersion))
            {
                throw new ArgumentException(
                    "Captured inventory version must not be empty.",
                    nameof(capturedInventoryVersion));
            }

            if (countedAt == default || countedAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Count time must be a non-default UTC value.",
                    nameof(countedAt));
            }

            if (countedByUserId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Counting user ID must not be empty.",
                    nameof(countedByUserId));
            }

            if (string.IsNullOrWhiteSpace(notes))
            {
                throw new ArgumentException(
                    "Observation notes must not be empty.",
                    nameof(notes));
            }

            Id = Guid.NewGuid();
            InventoryId = inventoryId;
            ProductId = productId;
            ProductSku = productSku.Trim().ToUpperInvariant();
            ProductName = productName.Trim();
            CapturedQuantityOnHand = capturedQuantityOnHand;
            CountedQuantity = countedQuantity;
            CapturedInventoryVersion = capturedInventoryVersion;
            CountedAt = countedAt;
            CountedByUserId = countedByUserId;
            Notes = notes.Trim();
        }
    }
}