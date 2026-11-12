using StockFlow.Application.PurchaseOrders.GetPurchaseOrders;

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
            CancellationToken cancellationToken = default);
    }
}
