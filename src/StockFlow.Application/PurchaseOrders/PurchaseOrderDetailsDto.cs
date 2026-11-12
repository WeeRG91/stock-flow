using StockFlow.Domain.Enums;

namespace StockFlow.Application.PurchaseOrders
{
    public sealed record PurchaseOrderDetailsDto(
        Guid Id,
        string OrderNumber,
        Guid SupplierId,
        string SupplierCode,
        string SupplierName,
        Guid WarehouseId,
        string WarehouseCode,
        string WarehouseName,
        PurchaseOrderStatus Status,
        DateTimeOffset CreatedAt,
        Guid CreatedByUserId,
        DateTimeOffset? SubmittedAt,
        DateTimeOffset? ApprovedAt,
        DateTimeOffset? RejectedAt,
        Guid Version,
        string Currency,
        IReadOnlyList<PurchaseOrderLineDto> Lines)
    {
        public decimal TotalAmount => Lines.Sum(line => line.LineTotal);
        public Guid? ApprovedByUserId { get; init; }
        public Guid? RejectedByUserId { get; init; }
    }
}
