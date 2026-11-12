namespace StockFlow.Application.Exceptions
{
    public sealed class PurchaseOrderConflictException : Exception
    {
        public Guid PurchaseOrderId { get; }

        public PurchaseOrderConflictException(
            Guid purchaseOrderId,
            string message)
            : base(message)
        {
            PurchaseOrderId = purchaseOrderId;
        }
    }
}
