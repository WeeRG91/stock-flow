namespace StockFlow.Application.GoodsReceipts.ConfirmGoodsReceipt
{
    public sealed record ConfirmGoodsReceiptCommand(
        Guid ReceiptId,
        Guid PurchaseOrderId,
        Guid ExpectedVersion,
        IReadOnlyList<ConfirmGoodsReceiptLineCommand> Lines);
}
