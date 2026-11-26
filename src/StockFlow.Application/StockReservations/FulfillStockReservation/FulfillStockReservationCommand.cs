namespace StockFlow.Application.StockReservations.FulfillStockReservation
{
    public sealed record FulfillStockReservationCommand(
        Guid ReservationId,
        Guid ExpectedVersion);
}
