using StockFlow.Domain.Enums;

namespace StockFlow.Domain.Entities
{
    public sealed class Transfer
    {
        private readonly List<TransferLine> _lines = new();
        private readonly List<TransferReceipt> _pendingReceipts = new();

        public Guid Id { get; }
        public Guid SourceWarehouseId { get; }
        public string SourceWarehouseCode { get; }
        public string SourceWarehouseName { get; }
        public Guid DestinationWarehouseId { get; }
        public string DestinationWarehouseCode { get; }
        public string DestinationWarehouseName { get; }
        public DateTimeOffset CreatedAt { get; }
        public Guid CreatedByUserId { get; }
        public DateTimeOffset? DispatchedAt { get; private set; }
        public Guid? DispatchedByUserId { get; private set; }
        public DateTimeOffset? CancelledAt { get; private set; }
        public Guid? CancelledByUserId { get; private set; }
        public DateTimeOffset? ReceivedAt { get; private set; }
        public TransferStatus Status { get; private set; }

        public IReadOnlyList<TransferLine> Lines => _lines.AsReadOnly();
        public IReadOnlyList<TransferReceipt> PendingReceipts => _pendingReceipts.AsReadOnly();

        public Transfer(
            Guid sourceWarehouseId,
            string sourceWarehouseCode,
            string sourceWarehouseName,
            Guid destinationWarehouseId,
            string destinationWarehouseCode,
            string destinationWarehouseName,
            DateTimeOffset createdAt,
            Guid createdByUserId)
        {
            if (sourceWarehouseId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Source warehouse ID must not be empty.",
                    nameof(sourceWarehouseId));
            }

            SourceWarehouseCode = NormalizeRequiredText(
                sourceWarehouseCode, nameof(sourceWarehouseCode)).ToUpperInvariant();

            SourceWarehouseName = NormalizeRequiredText(
                sourceWarehouseName, nameof(sourceWarehouseName));

            if (destinationWarehouseId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Destination warehouse ID must not be empty.",
                    nameof(destinationWarehouseId));
            }

            DestinationWarehouseCode = NormalizeRequiredText(
                destinationWarehouseCode, nameof(destinationWarehouseCode)).ToUpperInvariant();

            DestinationWarehouseName = NormalizeRequiredText(
                destinationWarehouseName, nameof(destinationWarehouseName));

            if (sourceWarehouseId == destinationWarehouseId)
            {
                throw new ArgumentException(
                    "Source and destination warehouses must differ.",
                    nameof(destinationWarehouseId));
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
            SourceWarehouseId = sourceWarehouseId;
            DestinationWarehouseId = destinationWarehouseId;
            CreatedAt = createdAt;
            CreatedByUserId = createdByUserId;
            Status = TransferStatus.Draft;
        }

        public Guid AddLine(
            Guid productId,
            string productSku,
            string productName,
            int quantity)
        {
            EnsureDraft();

            if (_lines.Any(line => line.ProductId == productId))
            {
                throw new InvalidOperationException(
                    "This product already has a line on the transfer.");
            }

            var line = new TransferLine(
                productId, productSku, productName, quantity);

            _lines.Add(line);
            return line.Id;
        }

        public void ReplaceDraftLines(IReadOnlyList<TransferLineDraft> drafts)
        {
            EnsureDraft();
            ArgumentNullException.ThrowIfNull(drafts);

            var existingByProduct = _lines.ToDictionary(line => line.ProductId);
            var productIds = new HashSet<Guid>();
            var replacements = new List<(TransferLine Line, int Quantity)>();

            foreach (var draft in drafts)
            {
                if (draft is null)
                {
                    throw new ArgumentException(
                        "Draft lines must not be null.", nameof(drafts));
                }

                if (draft.ProductId == Guid.Empty)
                {
                    throw new ArgumentException(
                        "Product ID must not be empty.", nameof(drafts));
                }

                if (!productIds.Add(draft.ProductId))
                {
                    throw new ArgumentException(
                        "A product can appear only once per transfer.", nameof(drafts));
                }

                if (draft.Quantity <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(drafts), "Quantity must be greater than zero.");
                }

                if (existingByProduct.TryGetValue(draft.ProductId, out var existing))
                {
                    replacements.Add((existing, draft.Quantity));
                }
                else
                {
                    var newLine = new TransferLine(
                        draft.ProductId,
                        draft.ProductSku,
                        draft.ProductName,
                        draft.Quantity);

                    replacements.Add((newLine, draft.Quantity));
                }
            }

            foreach (var (line, quantity) in replacements)
            {
                line.UpdateQuantity(quantity);
            }

            _lines.Clear();
            _lines.AddRange(replacements.Select(item => item.Line));
        }

        public void Dispatch(DateTimeOffset dispatchedAt, Guid dispatchedByUserId)
        {
            EnsureDraft();

            if (_lines.Count == 0)
            {
                throw new InvalidOperationException(
                    "A transfer must have at least one line before dispatch.");
            }

            if (dispatchedAt == default || dispatchedAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Dispatch time must be a non-default UTC value.",
                    nameof(dispatchedAt));
            }

            if (dispatchedAt < CreatedAt)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dispatchedAt),
                    "Dispatch time cannot precede creation.");
            }

            if (dispatchedByUserId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Dispatching user ID must not be empty.",
                    nameof(dispatchedByUserId));
            }

            foreach (var line in _lines)
            {
                line.Dispatch();
            }

            DispatchedAt = dispatchedAt;
            DispatchedByUserId = dispatchedByUserId;
            Status = TransferStatus.Dispatched;
        }

        public void Cancel(DateTimeOffset cancelledAt, Guid cancelledByUserId)
        {
            EnsureDraft();

            if (cancelledAt == default || cancelledAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Cancellation time must be a non-default UTC value.",
                    nameof(cancelledAt));
            }

            if (cancelledAt < CreatedAt)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cancelledAt),
                    "Cancellation time cannot precede creation.");
            }

            if (cancelledByUserId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Cancelling user ID must not be empty.",
                    nameof(cancelledByUserId));
            }

            CancelledAt = cancelledAt;
            CancelledByUserId = cancelledByUserId;
            Status = TransferStatus.Cancelled;
        }

        public void ApplyReceipt(TransferReceipt receipt)
        {
            ArgumentNullException.ThrowIfNull(receipt);

            if (receipt.TransferId != Id)
            {
                throw new ArgumentException(
                    "The receipt belongs to a different transfer.",
                    nameof(receipt));
            }

            if (_pendingReceipts.Any(existing => existing.Id == receipt.Id))
            {
                throw new InvalidOperationException(
                    "This receipt has already been applied to the transfer.");
            }

            if (Status is not (TransferStatus.Dispatched or TransferStatus.PartiallyReceived))
            {
                throw new InvalidOperationException(
                    "Only dispatched or partially received transfers can receive goods.");
            }

            var dispatchedAt = DispatchedAt
                ?? throw new InvalidOperationException(
                    "A transfer must have a dispatch timestamp before receiving.");

            if (receipt.ConfirmedAt < dispatchedAt)
            {
                throw new ArgumentException(
                    "Receipt confirmation cannot precede dispatch.",
                    nameof(receipt));
            }

            var linesById = _lines.ToDictionary(line => line.Id);

            foreach (var receiptLine in receipt.Lines)
            {
                if (!linesById.TryGetValue(receiptLine.TransferLineId, out var line))
                {
                    throw new ArgumentException(
                        "Every receipt line must belong to this transfer.",
                        nameof(receipt));
                }

                if (receiptLine.ReceivedQuantity > line.InTransitQuantity)
                {
                    throw new InvalidOperationException(
                        "Received quantity exceeds the quantity in transit.");
                }
            }

            foreach (var receiptLine in receipt.Lines)
            {
                linesById[receiptLine.TransferLineId]
                    .RecordReceivedQuantity(receiptLine.ReceivedQuantity);
            }

            var isComplete = _lines.All(line => line.InTransitQuantity == 0);
            Status = isComplete
                ? TransferStatus.Received
                : TransferStatus.PartiallyReceived;

            if (isComplete)
            {
                ReceivedAt = receipt.ConfirmedAt;
            }

            _pendingReceipts.Add(receipt);
        }

        private void EnsureDraft()
        {
            if (Status != TransferStatus.Draft)
            {
                throw new InvalidOperationException(
                    "Transfer lines can only be modified in Draft status.");
            }
        }

        private static string NormalizeRequiredText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Value must not be empty or whitespace.",
                    parameterName);
            }

            return value.Trim();
        }
    }
}
