namespace StockFlow.Domain.Entities
{
    public sealed class TransferReceipt
    {
        public Guid Id { get; }
        public Guid TransferId { get; }
        public DateTimeOffset ConfirmedAt { get; }
        public Guid ConfirmedByUserId { get; }
        public IReadOnlyList<TransferReceiptLine> Lines { get; }

        public TransferReceipt(
            Guid id,
            Guid transferId,
            DateTimeOffset confirmedAt,
            Guid confirmedByUserId,
            IReadOnlyList<TransferReceiptLine> lines)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException(
                    "Receipt ID must not be empty.", nameof(id));
            }

            if (transferId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Transfer ID must not be empty.", nameof(transferId));
            }

            if (confirmedAt == default || confirmedAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Confirmation time must be a non-default UTC value.",
                    nameof(confirmedAt));
            }

            if (confirmedByUserId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Confirming user ID must not be empty.",
                    nameof(confirmedByUserId));
            }

            ArgumentNullException.ThrowIfNull(lines);
            var receiptLines = lines.ToArray();

            if (receiptLines.Length == 0)
            {
                throw new ArgumentException(
                    "A receipt must have at least one line.", nameof(lines));
            }

            var lineIds = new HashSet<Guid>();

            foreach (var line in receiptLines)
            {
                if (line is null)
                {
                    throw new ArgumentException(
                        "Receipt lines must not be null.", nameof(lines));
                }

                if (!lineIds.Add(line.TransferLineId))
                {
                    throw new ArgumentException(
                        "A transfer line can appear only once per receipt.",
                        nameof(lines));
                }
            }

            Id = id;
            TransferId = transferId;
            ConfirmedAt = confirmedAt;
            ConfirmedByUserId = confirmedByUserId;
            Lines = Array.AsReadOnly(receiptLines);
        }
    }
}
