using StockFlow.Domain.Enums;

namespace StockFlow.Application.StockReservations.CreateStockReservation
{
    public sealed record CreateStockReservationResult(
        Guid ReservationId,
        Guid InventoryId,
        int Quantity,
        string Reference,
        StockReservationStatus Status,
        DateTimeOffset CreatedAt,
        Guid CreatedByUserId);
}
