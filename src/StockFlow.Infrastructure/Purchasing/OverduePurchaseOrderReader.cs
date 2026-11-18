using Microsoft.EntityFrameworkCore;
using StockFlow.Application.PurchaseOrders.MonitorOverduePurchaseOrders;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using StockFlow.Infrastructure.Persistence;

namespace StockFlow.Infrastructure.Purchasing
{
    public sealed class OverduePurchaseOrderReader(StockFlowDbContext context) : IOverduePurchaseOrderReader
    {
        public async Task<IReadOnlyList<PurchaseOrder>> GetCandidatesAsync(
            DateOnly today,
            CancellationToken cancellationToken = default)
        {
            return await context.PurchaseOrders
                .AsNoTracking()
                .Where(order =>
                    (order.Status == PurchaseOrderStatus.Approved ||
                    order.Status == PurchaseOrderStatus.PartiallyReceived) &&
                    order.ExpectedDeliveryDate.HasValue &&
                    order.ExpectedDeliveryDate.Value < today)
                .Include(order => order.Lines)
                .ToListAsync(cancellationToken);
        }
    }
}
