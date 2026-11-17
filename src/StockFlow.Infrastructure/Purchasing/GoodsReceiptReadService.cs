using Microsoft.EntityFrameworkCore;
using StockFlow.Application.GoodsReceipts;
using StockFlow.Application.GoodsReceipts.GetGoodsReceipts;
using StockFlow.Infrastructure.Persistence;

namespace StockFlow.Infrastructure.Purchasing
{
    public sealed class GoodsReceiptReadService : IGoodsReceiptReadService
    {
        private readonly StockFlowDbContext _context;

        public GoodsReceiptReadService(StockFlowDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        public async Task<IReadOnlyList<GoodsReceiptHistoryItemDto>?> GetByPurchaseOrderIdAsync(
            Guid purchaseOrderId,
            CancellationToken cancellationToken = default)
        {
            var orderExists = await _context.PurchaseOrders.AnyAsync(
                order => order.Id == purchaseOrderId, cancellationToken);

            if (!orderExists)
            {
                return null;
            }

            return await _context.GoodsReceipts
                .AsNoTracking()
                .AsSingleQuery()
                .Where(receipt => receipt.PurchaseOrderId == purchaseOrderId)
                .OrderByDescending(receipt => receipt.ConfirmedAt)
                .ThenBy(receipt => receipt.Id)
                .Select(receipt => new GoodsReceiptHistoryItemDto(
                    receipt.Id,
                    receipt.ConfirmedAt,
                    receipt.Lines
                        .OrderBy(line => line.PurchaseOrderLineId)
                        .Select(line => new GoodsReceiptHistoryLineDto(
                            line.PurchaseOrderLineId,
                            line.ReceivedQuantity))
                        .ToList()))
                .ToListAsync(cancellationToken);
        }
    }
}
