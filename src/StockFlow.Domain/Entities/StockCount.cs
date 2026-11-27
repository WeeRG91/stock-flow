using StockFlow.Domain.Enums;

namespace StockFlow.Domain.Entities
{
    public sealed class StockCount
    {
        private readonly List<StockCountLine> _lines = new();

        public Guid Id { get; }
        public Guid WarehouseId { get; }
        public string WarehouseCode { get; }
        public string WarehouseName { get; }
        public string Reference { get; }
        public DateTimeOffset CreatedAt { get; }
        public Guid CreatedByUserId { get; }
        public DateTimeOffset? SubmittedAt { get; private set; }
        public Guid? SubmittedByUserId { get; private set; }
        public DateTimeOffset? ReviewedAt { get; private set; }
        public Guid? ReviewedByUserId { get; private set; }
        public string? ReviewReason { get; private set; }
        public DateTimeOffset? AppliedAt { get; private set; }
        public Guid? AppliedByUserId { get; private set; }

        public StockCountStatus Status { get; private set; }

        public IReadOnlyList<StockCountLine> Lines => _lines.AsReadOnly();

        public StockCount(
            Guid warehouseId,
            string warehouseCode,
            string warehouseName,
            string reference,
            DateTimeOffset createdAt,
            Guid createdByUserId)
        {
            if (warehouseId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Warehouse ID must not be empty.",
                    nameof(warehouseId));
            }

            if (string.IsNullOrWhiteSpace(warehouseCode))
            {
                throw new ArgumentException(
                    "Warehouse code must not be empty.",
                    nameof(warehouseCode));
            }

            if (string.IsNullOrWhiteSpace(warehouseName))
            {
                throw new ArgumentException(
                    "Warehouse name must not be empty.",
                    nameof(warehouseName));
            }

            if (string.IsNullOrWhiteSpace(reference))
            {
                throw new ArgumentException(
                    "Reference must not be empty.",
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
            WarehouseId = warehouseId;
            WarehouseCode = warehouseCode.Trim().ToUpperInvariant();
            WarehouseName = warehouseName.Trim();
            Reference = reference.Trim();
            CreatedAt = createdAt;
            CreatedByUserId = createdByUserId;
            Status = StockCountStatus.Draft;
        }

        public Guid AddLine(
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
            EnsureDraft();

            if (_lines.Any(line => line.ProductId == productId))
            {
                throw new InvalidOperationException(
                    "This product already has a line on the stock count.");
            }

            if (_lines.Any(line => line.InventoryId == inventoryId))
            {
                throw new InvalidOperationException(
                    "This inventory already has a line on the stock count.");
            }

            var line = new StockCountLine(
                inventoryId,
                productId,
                productSku,
                productName,
                capturedQuantityOnHand,
                countedQuantity,
                capturedInventoryVersion,
                countedAt,
                countedByUserId,
                notes);

            if (line.CountedAt < CreatedAt)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(countedAt),
                    "Count time cannot precede session creation.");
            }

            _lines.Add(line);
            return line.Id;
        }

        public void RemoveLine(Guid lineId)
        {
            EnsureDraft();

            if (lineId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Line ID must not be empty.",
                    nameof(lineId));
            }

            var index = _lines.FindIndex(line => line.Id == lineId);

            if (index < 0)
            {
                throw new InvalidOperationException(
                    "The line does not belong to this stock count.");
            }

            _lines.RemoveAt(index);
        }

        public void Submit(
            DateTimeOffset submittedAt,
            Guid submittedByUserId)
        {
            EnsureDraft();

            if (_lines.Count == 0)
            {
                throw new InvalidOperationException(
                    "A stock count must contain at least one line before submission.");
            }

            if (submittedAt == default || submittedAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Submission time must be a non-default UTC value.",
                    nameof(submittedAt));
            }

            if (submittedAt < CreatedAt ||
                _lines.Any(line => line.CountedAt > submittedAt))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(submittedAt),
                    "Submission time must not precede creation or any observation.");
            }

            if (submittedByUserId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Submitting user ID must not be empty.",
                    nameof(submittedByUserId));
            }

            SubmittedAt = submittedAt;
            SubmittedByUserId = submittedByUserId;
            Status = StockCountStatus.Submitted;
        }

        public void Approve(
            DateTimeOffset reviewedAt,
            Guid reviewedByUserId,
            string reason)
        {
            ValidateReview(reviewedAt, reviewedByUserId, reason);

            ReviewedAt = reviewedAt;
            ReviewedByUserId = reviewedByUserId;
            ReviewReason = reason.Trim();
            Status = StockCountStatus.Approved;
        }

        public void Reject(
            DateTimeOffset reviewedAt,
            Guid reviewedByUserId,
            string reason)
        {
            ValidateReview(reviewedAt, reviewedByUserId, reason);

            ReviewedAt = reviewedAt;
            ReviewedByUserId = reviewedByUserId;
            ReviewReason = reason.Trim();
            Status = StockCountStatus.Rejected;
        }

        public void MarkApplied(
            DateTimeOffset appliedAt,
            Guid appliedByUserId)
        {
            if (Status != StockCountStatus.Approved)
            {
                throw new InvalidOperationException(
                    "Only Approved stock counts can be applied.");
            }

            var approvedAt = ReviewedAt
                ?? throw new InvalidOperationException(
                    "An approved stock count must have a review timestamp.");

            if (appliedAt == default || appliedAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Application time must be a non-default UTC value.",
                    nameof(appliedAt));
            }

            if (appliedAt < approvedAt)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(appliedAt),
                    "Application time cannot precede approval.");
            }

            if (appliedByUserId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Applying user ID must not be empty.",
                    nameof(appliedByUserId));
            }

            AppliedAt = appliedAt;
            AppliedByUserId = appliedByUserId;
            Status = StockCountStatus.Applied;
        }

        private void ValidateReview(
            DateTimeOffset reviewedAt,
            Guid reviewedByUserId,
            string reason)
        {
            if (Status != StockCountStatus.Submitted)
            {
                throw new InvalidOperationException(
                    "Only Submitted stock counts can be reviewed.");
            }

            var submittedAt = SubmittedAt
                ?? throw new InvalidOperationException(
                    "A submitted stock count must have a submission timestamp.");

            if (reviewedAt == default || reviewedAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Review time must be a non-default UTC value.",
                    nameof(reviewedAt));
            }

            if (reviewedAt < submittedAt)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(reviewedAt),
                    "Review time cannot precede submission.");
            }

            if (reviewedByUserId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Reviewer ID must not be empty.",
                    nameof(reviewedByUserId));
            }

            if (reviewedByUserId == SubmittedByUserId ||
                _lines.Any(line => line.CountedByUserId == reviewedByUserId))
            {
                throw new InvalidOperationException(
                    "The submitter and counting employees cannot review this stock count.");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException(
                    "Review reason must not be empty.",
                    nameof(reason));
            }
        }

        private void EnsureDraft()
        {
            if (Status != StockCountStatus.Draft)
            {
                throw new InvalidOperationException(
                    "Only Draft stock counts can be edited.");
            }
        }
    }
}
