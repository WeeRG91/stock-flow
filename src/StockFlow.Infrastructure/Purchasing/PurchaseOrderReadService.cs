using Microsoft.EntityFrameworkCore;
using StockFlow.Application.PurchaseOrders;
using StockFlow.Application.PurchaseOrders.GetPurchaseOrders;
using StockFlow.Infrastructure.Persistence;

namespace StockFlow.Infrastructure.Purchasing
{
    public sealed class PurchaseOrderReadService : IPurchaseOrderReadService
    {
        private readonly StockFlowDbContext _context;

        public PurchaseOrderReadService(StockFlowDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        public Task<PurchaseOrderDetailsDto?> GetByIdAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default)
        {
            return _context.PurchaseOrders
                .AsNoTracking()
                .AsSingleQuery()
                .Where(order => order.Id == purchaseOrderId)
                .Select(order => new PurchaseOrderDetailsDto(
                    order.Id,
                    order.OrderNumber,
                    order.SupplierId,
                    order.SupplierCode,
                    order.SupplierName,
                    order.WarehouseId,
                    order.WarehouseCode,
                    order.WarehouseName,
                    order.Status,
                    order.CreatedAt,
                    order.CreatedByUserId,
                    order.SubmittedAt,
                    order.ApprovedAt,
                    order.RejectedAt,
                    EF.Property<Guid>(order, "Version"),
                    "EUR",
                    order.Lines
                        .OrderBy(line => line.ProductSku)
                        .ThenBy(line => line.Id)
                        .Select(line => new PurchaseOrderLineDto(
                            line.Id,
                            line.ProductId,
                            line.ProductSku,
                            line.ProductName,
                            line.OrderedQuantity,
                            line.UnitPrice))
                        .ToList())
                {
                    ApprovedByUserId = order.ApprovedByUserId,
                    RejectedByUserId = order.RejectedByUserId
                })
                .SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<GetPurchaseOrdersResult> GetPageAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var orders = _context.PurchaseOrders.AsNoTracking();
            var offset = checked((int)(((long)pageNumber - 1) * pageSize));

            var totalCount = await orders.CountAsync(cancellationToken);

            var items = await orders
                .OrderByDescending(order => order.CreatedAt)
                .ThenByDescending(order => order.Id)
                .Skip(offset)
                .Take(pageSize)
                .Select(order => new PurchaseOrderSummaryDto(
                    order.Id,
                    order.OrderNumber,
                    order.SupplierCode,
                    order.SupplierName,
                    order.WarehouseCode,
                    order.WarehouseName,
                    order.Status,
                    order.CreatedAt,
                    order.Lines.Count))
                .ToListAsync(cancellationToken);

            return new GetPurchaseOrdersResult(
                items, pageNumber, pageSize, totalCount);
        }
    }
}
