namespace StockFlow.Application.Transfers.GetTransferReceipts
{
    public sealed record TransferReceiptHistoryItemDto(
        Guid ReceiptId,
        DateTimeOffset ConfirmedAt,
        Guid ConfirmedByUserId,
        IReadOnlyList<TransferReceiptHistoryLineDto> Lines);

    public sealed record TransferReceiptHistoryLineDto(
        Guid TransferLineId,
        int ReceivedQuantity);
}
