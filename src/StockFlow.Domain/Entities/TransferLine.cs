namespace StockFlow.Domain.Entities
{
    public sealed class TransferLine
    {
        public Guid Id { get; }
        public Guid ProductId { get; }
        public string ProductSku { get; }
        public string ProductName { get; }
        public int Quantity { get; private set; }
        public int DispatchedQuantity { get; private set; }
        public int ReceivedQuantity { get; private set; }
        public int InTransitQuantity => DispatchedQuantity - ReceivedQuantity;

        internal TransferLine(
            Guid productId,
            string productSku,
            string productName,
            int quantity)
        {
            if (productId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Product ID must not be empty.", nameof(productId));
            }

            if (string.IsNullOrWhiteSpace(productSku))
            {
                throw new ArgumentException(
                    "Product SKU must not be empty.", nameof(productSku));
            }

            if (string.IsNullOrWhiteSpace(productName))
            {
                throw new ArgumentException(
                    "Product name must not be empty.", nameof(productName));
            }

            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity), "Quantity must be greater than zero.");
            }

            Id = Guid.NewGuid();
            ProductId = productId;
            ProductSku = productSku.Trim().ToUpperInvariant();
            ProductName = productName.Trim();
            Quantity = quantity;
        }

        internal void UpdateQuantity(int quantity)
        {
            if (DispatchedQuantity != 0)
            {
                throw new InvalidOperationException(
                    "A dispatched transfer line cannot be edited.");
            }

            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    "Quantity must be greater than zero.");
            }

            Quantity = quantity;
        }

        internal void Dispatch()
        {
            if (DispatchedQuantity != 0)
            {
                throw new InvalidOperationException(
                    "This transfer line has already been dispatched.");
            }

            DispatchedQuantity = Quantity;
        }

        internal void RecordReceivedQuantity(int quantity)
        {
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity), "Received quantity must be greater than zero.");
            }

            if (quantity > InTransitQuantity)
            {
                throw new InvalidOperationException(
                    "Received quantity exceeds the quantity in transit.");
            }

            ReceivedQuantity += quantity;
        }
    }
}
