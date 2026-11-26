namespace StockFlow.Application.Exceptions
{
    public sealed class StockReservationConflictException : Exception
    {
        public Guid ReservationId { get; }

        public StockReservationConflictException(
            Guid reservationId,
            string message)
            : base(message)
        {
            ReservationId = reservationId;
        }
    }
}
