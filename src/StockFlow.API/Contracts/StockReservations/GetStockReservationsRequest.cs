using StockFlow.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace StockFlow.API.Contracts.StockReservations
{
    public sealed class GetStockReservationsRequest
    {
        [Range(1, int.MaxValue)]
        public int PageNumber { get; init; } = 1;

        [Range(1, 100)]
        public int PageSize { get; init; } = 20;

        public StockReservationStatus? Status { get; init; }

        public string? Search { get; init; }

        public Guid? InventoryId { get; init; }
    }
}
