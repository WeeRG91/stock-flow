using System.ComponentModel.DataAnnotations;

namespace StockFlow.API.Contracts.StockReservations
{
    public sealed class StockReservationVersionRequest
    {
        [Required]
        public Guid? ExpectedVersion { get; init; }
    }
}