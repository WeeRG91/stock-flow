namespace StockFlow.Application.GoodsReceipts.GetGoodsReceipts
{
    public sealed record GoodsReceiptHistoryItemDto(
        Guid ReceiptId,
        DateTimeOffset ConfirmedAt,
        IReadOnlyList<GoodsReceiptHistoryLineDto> Lines);

    public sealed record GoodsReceiptHistoryLineDto(
        Guid PurchaseOrderLineId,
        int ReceivedQuantity);
}
