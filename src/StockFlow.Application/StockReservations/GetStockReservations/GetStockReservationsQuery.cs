using StockFlow.Domain.Enums;

namespace StockFlow.Application.StockReservations.GetStockReservations
{
    public sealed record GetStockReservationsQuery(
        int PageNumber = 1,
        int PageSize = 20,
        StockReservationStatus? Status = null,
        string? Search = null,
        Guid? InventoryId = null);
}
