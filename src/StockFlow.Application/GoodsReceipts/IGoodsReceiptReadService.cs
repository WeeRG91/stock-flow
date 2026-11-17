using StockFlow.Application.GoodsReceipts.GetGoodsReceipts;

namespace StockFlow.Application.GoodsReceipts
{
    public interface IGoodsReceiptReadService
    {
        // Returns null for a missing order and an empty list for an order with no receipts.
        Task<IReadOnlyList<GoodsReceiptHistoryItemDto>?> GetByPurchaseOrderIdAsync(
            Guid purchaseOrderId,
            CancellationToken cancellationToken = default);
    }
}
