namespace StockFlow.Domain.Entities
{
    public sealed class GoodsReceipt
    {
        public Guid Id { get; }
        public Guid PurchaseOrderId { get; }
        public DateTimeOffset ConfirmedAt { get; }
        public Guid ConfirmedByUserId { get; }
        private readonly List<GoodsReceiptLine> _lines = new();
        public IReadOnlyList<GoodsReceiptLine> Lines => _lines.AsReadOnly();

        public GoodsReceipt(
            Guid id,
            Guid purchaseOrderId,
            DateTimeOffset confirmedAt,
            Guid confirmedByUserId,
            IReadOnlyList<GoodsReceiptLine> lines)
            : this(id, purchaseOrderId, confirmedAt, confirmedByUserId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Receipt ID must not be empty.", nameof(id));
            }

            if (purchaseOrderId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Purchase order ID must not be empty.", nameof(purchaseOrderId));
            }

            if (confirmedByUserId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Confirming user ID must not be empty.", nameof(confirmedByUserId));
            }

            if (confirmedAt == default || confirmedAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Confirmation time must be a valid UTC timestamp.", nameof(confirmedAt));
            }

            ArgumentNullException.ThrowIfNull(lines);
            var receiptLines = lines.ToArray();

            if (receiptLines.Length == 0)
            {
                throw new ArgumentException("A receipt must have at least one line.", nameof(lines));
            }

            var orderLineIds = new HashSet<Guid>();

            foreach (var line in receiptLines)
            {
                if (line is null)
                {
                    throw new ArgumentException("Receipt lines must not be null.", nameof(lines));
                }

                if (!orderLineIds.Add(line.PurchaseOrderLineId))
                {
                    throw new ArgumentException(
                        "A purchase order line can appear only once per receipt.", nameof(lines));
                }
            }

            Id = id;
            PurchaseOrderId = purchaseOrderId;
            ConfirmedAt = confirmedAt;
            ConfirmedByUserId = confirmedByUserId;
            _lines.AddRange(receiptLines);
        }

        private GoodsReceipt(
            Guid id,
            Guid purchaseOrderId,
            DateTimeOffset confirmedAt,
            Guid confirmedByUserId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Receipt ID must not be empty.", nameof(id));
            }

            if (purchaseOrderId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Purchase order ID must not be empty.", nameof(purchaseOrderId));
            }

            if (confirmedByUserId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Confirming user ID must not be empty.", nameof(confirmedByUserId));
            }

            if (confirmedAt == default || confirmedAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Confirmation time must be a valid UTC timestamp.", nameof(confirmedAt));
            }

            Id = id;
            PurchaseOrderId = purchaseOrderId;
            ConfirmedAt = confirmedAt;
            ConfirmedByUserId = confirmedByUserId;
        }
    }
}
