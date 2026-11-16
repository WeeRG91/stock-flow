namespace StockFlow.Domain.Entities
{
    public sealed class PurchaseOrderLine
    {
        public Guid Id { get; }
        public Guid ProductId { get; }
        public string ProductSku { get; }
        public string ProductName { get; }
        public int OrderedQuantity { get; }
        public int ReceivedQuantity { get; private set; }
        public int RemainingQuantity => OrderedQuantity - ReceivedQuantity;
        public decimal UnitPrice { get; }
        public decimal LineTotal => checked(OrderedQuantity * UnitPrice);

        internal PurchaseOrderLine(
            Guid productId,
            string productSku,
            string productName,
            int orderedQuantity,
            decimal unitPrice)
            : this(
                Guid.NewGuid(),
                productId,
                productSku,
                productName,
                orderedQuantity,
                unitPrice)
        {}

        internal PurchaseOrderLine WithUpdatedTerms(
            int orderedQuantity,
            decimal unitPrice)
        {
            return new PurchaseOrderLine(
                Id,
                ProductId,
                ProductSku,
                ProductName,
                orderedQuantity,
                unitPrice);
        }

        internal void RecordReceivedQuantity(int quantity)
        {
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    "Received quantity must be greater than zero.");
            }

            if (quantity > RemainingQuantity)
            {
                throw new InvalidOperationException(
                    "Received quantity exceeds the remaining ordered quantity.");
            }

            ReceivedQuantity += quantity;
        }

        private PurchaseOrderLine(
            Guid id,
            Guid productId,
            string productSku,
            string productName,
            int orderedQuantity,
            decimal unitPrice)
        {
            if (productId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Product ID must not be empty.",
                    nameof(productId));
            }

            if (string.IsNullOrWhiteSpace(productSku))
            {
                throw new ArgumentException(
                    "Product SKU must not be empty or whitespace.",
                    nameof(productSku));
            }

            if (string.IsNullOrWhiteSpace(productName))
            {
                throw new ArgumentException(
                    "Product name must not be empty or whitespace.",
                    nameof(productName));
            }

            if (orderedQuantity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(orderedQuantity),
                    "Ordered quantity must be greater than zero.");
            }

            if (unitPrice < 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(unitPrice),
                    "Unit price must not be negative.");
            }

            if (decimal.Round(unitPrice, 2) != unitPrice)
            {
                throw new ArgumentException(
                    "Unit price must have at most two decimal places.",
                    nameof(unitPrice));
            }

            _ = checked(orderedQuantity * unitPrice);

            Id = id;
            ProductId = productId;
            ProductSku = productSku.Trim().ToUpperInvariant();
            ProductName = productName.Trim();
            OrderedQuantity = orderedQuantity;
            UnitPrice = unitPrice;
        }
    }
}
