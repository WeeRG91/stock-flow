using StockFlow.Domain.Enums;

namespace StockFlow.Domain.Entities
{
    public sealed class PurchaseOrder
    {
        private readonly List<PurchaseOrderLine> _lines = new();
        public const int MaxOrderNumberLength = 64;

        public Guid Id { get; }
        public string OrderNumber { get; }
        public Guid SupplierId { get; }
        public string SupplierCode { get; }
        public string SupplierName { get; }
        public Guid WarehouseId { get; }
        public string WarehouseCode { get; }
        public string WarehouseName { get; }
        public string Currency => "EUR";

        public PurchaseOrderStatus Status { get; private set; }
        public DateTimeOffset CreatedAt { get; }
        public DateTimeOffset? SubmittedAt { get; private set; }
        public DateTimeOffset? ApprovedAt { get; private set; }
        public DateTimeOffset? RejectedAt { get; private set; }

        public IReadOnlyList<PurchaseOrderLine> Lines => _lines.AsReadOnly();
        public decimal TotalAmount => _lines.Sum(line => line.LineTotal);

        public Guid CreatedByUserId { get; }
        public Guid? ApprovedByUserId { get; private set; }
        public Guid? RejectedByUserId { get; private set; }

        public PurchaseOrder(
            string orderNumber,
            Guid supplierId,
            string supplierCode,
            string supplierName,
            Guid warehouseId,
            string warehouseCode,
            string warehouseName,
            DateTimeOffset createdAt,
            Guid createdByUserId)
        {
            if (string.IsNullOrWhiteSpace(orderNumber))
            {
                throw new ArgumentException(
                    "Order number must not be empty or whitespace.",
                    nameof(orderNumber));
            }

            var normalizedOrderNumber =
                orderNumber.Trim().ToUpperInvariant();

            if (normalizedOrderNumber.Length > MaxOrderNumberLength)
            {
                throw new ArgumentException(
                    $"Order number must not exceed {MaxOrderNumberLength} characters.",
                    nameof(orderNumber));
            }

            if (supplierId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Supplier ID must not be empty.",
                    nameof(supplierId));
            }

            var normalizedSupplierCode =
                NormalizeRequiredText(supplierCode, nameof(supplierCode))
                    .ToUpperInvariant();

            var normalizedSupplierName =
                NormalizeRequiredText(supplierName, nameof(supplierName));

            if (warehouseId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Warehouse ID must not be empty.",
                    nameof(warehouseId));
            }

            var normalizedWarehouseCode =
                NormalizeRequiredText(warehouseCode, nameof(warehouseCode))
                    .ToUpperInvariant();

            var normalizedWarehouseName =
                NormalizeRequiredText(warehouseName, nameof(warehouseName));

            if (createdAt == default)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(createdAt),
                    "Creation timestamp must not be the default value.");
            }

            if (createdAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Creation timestamp must use UTC.",
                    nameof(createdAt));
            }

            if (createdByUserId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Creator user ID must not be empty.",
                    nameof(createdByUserId));
            }

            Id = Guid.NewGuid();
            OrderNumber = normalizedOrderNumber;
            SupplierId = supplierId;
            SupplierCode = normalizedSupplierCode;
            SupplierName = normalizedSupplierName;
            WarehouseId = warehouseId;
            WarehouseCode = normalizedWarehouseCode;
            WarehouseName = normalizedWarehouseName;
            CreatedAt = createdAt;
            Status = PurchaseOrderStatus.Draft;
            CreatedByUserId = createdByUserId;
        }

        public Guid AddLine(
            Guid productId,
            string productSku,
            string productName,
            int orderedQuantity,
            decimal unitPrice)
        {
            EnsureDraft();

            if (_lines.Any(line => line.ProductId == productId))
            {
                throw new InvalidOperationException(
                    "The product already has a line on this purchase order.");
            }

            var line = new PurchaseOrderLine(
                productId,
                productSku,
                productName,
                orderedQuantity,
                unitPrice);

            _ = checked(TotalAmount + line.LineTotal);

            _lines.Add(line);

            return line.Id;
        }

        public void UpdateLine(
            Guid lineId,
            int orderedQuantity,
            decimal unitPrice)
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
                    "The line does not belong to this purchase order.");
            }

            var replacement = _lines[index].WithUpdatedTerms(
                orderedQuantity,
                unitPrice);

            var otherLinesTotal = _lines
                .Where(line => line.Id != lineId)
                .Sum(line => line.LineTotal);

            _ = checked(otherLinesTotal + replacement.LineTotal);

            _lines[index] = replacement;
        }

        public void ReplaceDraftLines(IReadOnlyList<PurchaseOrderLineDraft> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);
            EnsureDraft();

            var existingByProduct = _lines.ToDictionary(line => line.ProductId);
            var seenProducts = new HashSet<Guid>();
            var replacements = new List<PurchaseOrderLine>(lines.Count);

            foreach (var draft in lines)
            {
                ArgumentNullException.ThrowIfNull(draft);

                if (!seenProducts.Add(draft.ProductId))
                {
                    throw new ArgumentException(
                        "A product may appear only once per order.", nameof(lines));
                }

                if (existingByProduct.TryGetValue(draft.ProductId, out var existing))
                {
                    var replacement =
                        existing.OrderedQuantity == draft.OrderedQuantity &&
                        existing.UnitPrice == draft.UnitPrice
                            ? existing
                            : existing.WithUpdatedTerms(draft.OrderedQuantity, draft.UnitPrice);

                    replacements.Add(replacement);
                }
                else
                {
                    replacements.Add(new PurchaseOrderLine(
                        draft.ProductId,
                        draft.ProductSku,
                        draft.ProductName,
                        draft.OrderedQuantity,
                        draft.UnitPrice));
                }
            }

            _ = replacements.Sum(line => line.LineTotal);

            _lines.Clear();
            _lines.AddRange(replacements);
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
                    "The line does not belong to this purchase order.");
            }

            _lines.RemoveAt(index);
        }

        public void Submit(DateTimeOffset submittedAt)
        {
            if (Status != PurchaseOrderStatus.Draft)
            {
                throw new InvalidOperationException(
                    "Only Draft purchase orders can be submitted.");
            }

            if (_lines.Count == 0)
            {
                throw new InvalidOperationException(
                    "A purchase order must contain at least one line before submission.");
            }

            if (submittedAt == default)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(submittedAt),
                    "Submission timestamp must not be the default value.");
            }

            if (submittedAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Submission timestamp must use UTC.",
                    nameof(submittedAt));
            }

            if (submittedAt < CreatedAt)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(submittedAt),
                    "Submission timestamp must not precede creation.");
            }

            SubmittedAt = submittedAt;
            Status = PurchaseOrderStatus.PendingApproval;
        }

        public void Approve(DateTimeOffset approvedAt, Guid approvedByUserId)
        {
            if (Status != PurchaseOrderStatus.PendingApproval)
            {
                throw new InvalidOperationException(
                    "Only PendingApproval purchase orders can be approved.");
            }

            var submittedAt = SubmittedAt
                ?? throw new InvalidOperationException(
                    "A pending purchase order must have a submission timestamp.");

            if (approvedAt == default)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(approvedAt),
                    "Approval timestamp must not be the default value.");
            }

            if (approvedAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Approval timestamp must use UTC.",
                    nameof(approvedAt));
            }

            if (approvedAt < submittedAt)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(approvedAt),
                    "Approval timestamp must not precede submission.");
            }

            if (approvedByUserId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Approver ID must not be empty.", nameof(approvedByUserId));
            }

            ApprovedAt = approvedAt;
            ApprovedByUserId = approvedByUserId;
            Status = PurchaseOrderStatus.Approved;
        }

        public void Reject(DateTimeOffset rejectedAt, Guid rejectedByUserId)
        {
            if (Status != PurchaseOrderStatus.PendingApproval)
            {
                throw new InvalidOperationException(
                    "Only PendingApproval purchase orders can be rejected.");
            }

            var submittedAt = SubmittedAt
                ?? throw new InvalidOperationException(
                    "A pending purchase order must have a submission timestamp.");

            if (rejectedAt == default)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rejectedAt),
                    "Rejection timestamp must not be the default value.");
            }

            if (rejectedAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Rejection timestamp must use UTC.",
                    nameof(rejectedAt));
            }

            if (rejectedAt < submittedAt)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rejectedAt),
                    "Rejection timestamp must not precede submission.");
            }

            if (rejectedByUserId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Rejecting user ID must not be empty.", nameof(rejectedByUserId));
            }

            RejectedAt = rejectedAt;
            RejectedByUserId = rejectedByUserId;
            Status = PurchaseOrderStatus.Rejected;
        }

        private void EnsureDraft()
        {
            if (Status != PurchaseOrderStatus.Draft)
            {
                throw new InvalidOperationException(
                    "Purchase order lines can only be modified in Draft status.");
            }
        }

        private static string NormalizeRequiredText(
            string value,
            string parameterName)
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
