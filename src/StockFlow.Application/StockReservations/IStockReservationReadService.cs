using StockFlow.Application.StockReservations.GetStockReservations;
using StockFlow.Domain.Enums;

namespace StockFlow.Application.StockReservations
{
    public interface IStockReservationReadService
    {
        Task<StockReservationDto?> GetByIdAsync(
            Guid reservationId,
            CancellationToken cancellationToken = default);

        Task<GetStockReservationsResult> GetPageAsync(
            int pageNumber,
            int pageSize,
            StockReservationStatus? status,
            string? search,
            Guid? inventoryId,
            CancellationToken cancellationToken = default);
    }
}
