namespace StockFlow.Application.Exceptions
{
    public sealed class TransferConflictException : Exception
    {
        public Guid TransferId { get; }

        public TransferConflictException(Guid transferId, string message)
            : base(message)
        {
            TransferId = transferId;
        }
    }
}
