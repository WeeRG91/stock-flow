using StockFlow.Domain.Enums;

namespace StockFlow.Application.StockReservations
{
    public sealed record StockReservationDto(
        Guid ReservationId,
        Guid InventoryId,
        Guid ProductId,
        string ProductName,
        string Sku,
        Guid WarehouseId,
        string WarehouseName,
        string WarehouseCode,
        int Quantity,
        string Reference,
        StockReservationStatus Status,
        DateTimeOffset CreatedAt,
        Guid CreatedByUserId,
        DateTimeOffset? ReleasedAt,
        Guid? ReleasedByUserId,
        DateTimeOffset? FulfilledAt,
        Guid? FulfilledByUserId,
        Guid Version);
}
