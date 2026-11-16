namespace StockFlow.Application.GoodsReceipts.ConfirmGoodsReceipt
{
    public sealed record ConfirmGoodsReceiptResult(
        Guid ReceiptId,
        Guid PurchaseOrderId,
        DateTimeOffset ConfirmedAt,
        Guid ConfirmedByUserId,
        IReadOnlyList<ConfirmedGoodsReceiptLineDto> Lines);

    public sealed record ConfirmedGoodsReceiptLineDto(
        Guid PurchaseOrderLineId,
        int ReceivedQuantity);
}
