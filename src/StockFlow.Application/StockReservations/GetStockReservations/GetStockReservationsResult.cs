namespace StockFlow.Application.StockReservations.GetStockReservations
{
    public sealed record GetStockReservationsResult(
        IReadOnlyList<StockReservationDto> Items,
        int PageNumber,
        int PageSize,
        int TotalCount);
}
