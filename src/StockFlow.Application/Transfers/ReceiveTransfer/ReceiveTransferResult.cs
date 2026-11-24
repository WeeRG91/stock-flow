namespace StockFlow.Application.Transfers.ReceiveTransfer
{
    public sealed record ReceiveTransferResult(
        Guid ReceiptId,
        Guid TransferId,
        DateTimeOffset ConfirmedAt,
        Guid ConfirmedByUserId,
        IReadOnlyList<ReceivedTransferLineDto> Lines);

    public sealed record ReceivedTransferLineDto(
        Guid TransferLineId,
        int ReceivedQuantity);
}
