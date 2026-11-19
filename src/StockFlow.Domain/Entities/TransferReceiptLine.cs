namespace StockFlow.Domain.Entities
{
    public sealed class TransferReceiptLine
    {
        public Guid TransferLineId { get; }
        public int ReceivedQuantity { get; }

        public TransferReceiptLine(Guid transferLineId, int receivedQuantity)
        {
            if (transferLineId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Transfer line ID must not be empty.",
                    nameof(transferLineId));
            }

            if (receivedQuantity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(receivedQuantity),
                    "Received quantity must be greater than zero.");
            }

            TransferLineId = transferLineId;
            ReceivedQuantity = receivedQuantity;
        }
    }
}
