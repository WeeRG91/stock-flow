namespace StockFlow.Domain.Entities
{
    public sealed class GoodsReceiptLine
    {
        public Guid PurchaseOrderLineId { get; }
        public int ReceivedQuantity { get; }

        public GoodsReceiptLine(Guid purchaseOrderLineId, int receivedQuantity)
        {
            if (purchaseOrderLineId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Purchase order line ID must not be empty.",
                    nameof(purchaseOrderLineId));
            }

            if (receivedQuantity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(receivedQuantity),
                    "Received quantity must be greater than zero.");
            }

            PurchaseOrderLineId = purchaseOrderLineId;
            ReceivedQuantity = receivedQuantity;
        }
    }
}
