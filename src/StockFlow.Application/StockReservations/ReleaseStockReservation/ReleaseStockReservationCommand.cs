namespace StockFlow.Application.StockReservations.ReleaseStockReservation
{
    public sealed record ReleaseStockReservationCommand(
        Guid ReservationId,
        Guid ExpectedVersion);
}
