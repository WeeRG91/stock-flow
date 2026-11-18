namespace StockFlow.Domain.Entities
{
    public sealed class OverduePurchaseOrderAlert
    {
        public Guid Id { get; private set; }
        public Guid PurchaseOrderId { get; private set; }
        public DateTimeOffset DetectedAt { get; private set; }
        public DateTimeOffset? ResolvedAt { get; private set; }

        public bool IsActive => ResolvedAt is null;

        public OverduePurchaseOrderAlert(
            Guid purchaseOrderId,
            DateTimeOffset detectedAt)
        {
            if (purchaseOrderId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Purchase order ID must not be empty.", nameof(purchaseOrderId));
            }

            ValidateTimestamp(detectedAt, nameof(detectedAt));

            Id = Guid.NewGuid();
            PurchaseOrderId = purchaseOrderId;
            DetectedAt = detectedAt;
        }

        public void Resolve(DateTimeOffset resolvedAt)
        {
            if (!IsActive) return;

            ValidateTimestamp(resolvedAt, nameof(resolvedAt));

            if (resolvedAt < DetectedAt)
            {
                throw new ArgumentException(
                    "Resolution cannot precede detection.", nameof(resolvedAt));
            }

            ResolvedAt = resolvedAt;
        }

        private static void ValidateTimestamp(
            DateTimeOffset timestamp,
            string parameterName)
        {
            if (timestamp == default || timestamp.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Timestamp must be a non-default UTC value.", parameterName);
            }
        }
    }
}
