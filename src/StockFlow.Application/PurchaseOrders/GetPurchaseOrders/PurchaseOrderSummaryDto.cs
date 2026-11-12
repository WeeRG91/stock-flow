using StockFlow.Domain.Enums;

namespace StockFlow.Application.PurchaseOrders.GetPurchaseOrders
{
    public sealed record PurchaseOrderSummaryDto(
        Guid Id,
        string OrderNumber,
        string SupplierCode,
        string SupplierName,
        string WarehouseCode,
        string WarehouseName,
        PurchaseOrderStatus Status,
        DateTimeOffset CreatedAt,
        int LineCount);
}
