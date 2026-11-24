namespace StockFlow.Application.Transfers.ReceiveTransfer
{
    public sealed record ReceiveTransferCommand(
        Guid ReceiptId,
        Guid TransferId,
        Guid ExpectedVersion,
        IReadOnlyList<ReceiveTransferLineCommand> Lines);

    public sealed record ReceiveTransferLineCommand(
        Guid TransferLineId,
        int ReceivedQuantity);
}
