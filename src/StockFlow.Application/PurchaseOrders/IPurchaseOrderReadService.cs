using StockFlow.Application.PurchaseOrders.GetPurchaseOrders;
using StockFlow.Domain.Enums;

namespace StockFlow.Application.PurchaseOrders
{
    public interface IPurchaseOrderReadService
    {
        Task<PurchaseOrderDetailsDto?> GetByIdAsync(
            Guid purchaseOrderId,
            CancellationToken cancellationToken = default);

        Task<GetPurchaseOrdersResult> GetPageAsync(
            int pageNumber,
            int pageSize,
            PurchaseOrderStatus? status,
            string? search,
            CancellationToken cancellationToken = default);
    }
}
