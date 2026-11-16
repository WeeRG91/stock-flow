namespace StockFlow.Application.GoodsReceipts.ConfirmGoodsReceipt
{
    public sealed record ConfirmGoodsReceiptLineCommand(
        Guid PurchaseOrderLineId,
        int ReceivedQuantity);
}
