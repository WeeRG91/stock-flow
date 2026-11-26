namespace StockFlow.Application.StockReservations.CreateStockReservation
{
    public sealed record CreateStockReservationCommand(
        Guid RequestId,
        Guid InventoryId,
        int Quantity,
        string Reference);
}
