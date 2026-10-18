namespace StockFlow.Domain.Entities
{
    public sealed class LowStockAlert
    {
        public Guid Id { get; private set; }
        public Guid InventoryId { get; private set; }
        public DateTimeOffset DetectedAt { get; private set; }
        public DateTimeOffset? ResolvedAt { get; private set; }

        public bool IsActive => ResolvedAt is null;

        public LowStockAlert(
            Guid inventoryId,
            DateTimeOffset detectedAt)
        {
            if (inventoryId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Inventory ID must not be empty.",
                    nameof(inventoryId));
            }

            ValidateTimestamp(detectedAt, nameof(detectedAt));

            Id = Guid.NewGuid();
            InventoryId = inventoryId;
            DetectedAt = detectedAt;
        }

        public void Resolve(DateTimeOffset resolvedAt)
        {
            ValidateTimestamp(resolvedAt, nameof(resolvedAt));

            if (!IsActive) return;

            if (resolvedAt < DetectedAt)
            {
                throw new ArgumentException(
                    "Resolution cannot precede detection.",
                    nameof(resolvedAt));
            }

            ResolvedAt = resolvedAt;
        }

        public void Reopen(DateTimeOffset detectedAt)
        {
            ValidateTimestamp(detectedAt, nameof(detectedAt));

            if (IsActive) return;

            if (detectedAt < ResolvedAt!.Value)
            {
                throw new ArgumentException(
                    "Detection cannot precede the previous resolution.",
                    nameof(detectedAt));
            }

            DetectedAt = detectedAt;
            ResolvedAt = null;
        }

        private static void ValidateTimestamp(
            DateTimeOffset timestamp,
            string parameterName)
        {
            if (timestamp == default || timestamp.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Timestamp must be a non-default UTC value.",
                    parameterName);
            }
        }
    }
}
