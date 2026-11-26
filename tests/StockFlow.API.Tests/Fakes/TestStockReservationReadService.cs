using StockFlow.Application.StockReservations;
using StockFlow.Application.StockReservations.GetStockReservations;
using StockFlow.Domain.Enums;

namespace StockFlow.API.Tests.Fakes;

public sealed class TestStockReservationReadService : IStockReservationReadService
{
    public IReadOnlyList<StockReservationDto> Items { get; set; } = Array.Empty<StockReservationDto>();
    public int TotalCount { get; set; }
    public int PageCallCount { get; private set; }
    public int ReceivedPageNumber { get; private set; }
    public int ReceivedPageSize { get; private set; }
    public StockReservationStatus? ReceivedStatus { get; private set; }
    public string? ReceivedSearch { get; private set; }
    public Guid? ReceivedInventoryId { get; private set; }

    public Task<GetStockReservationsResult> GetPageAsync(
        int pageNumber, int pageSize, StockReservationStatus? status,
        string? search, Guid? inventoryId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PageCallCount++;
        ReceivedPageNumber = pageNumber;
        ReceivedPageSize = pageSize;
        ReceivedStatus = status;
        ReceivedSearch = search;
        ReceivedInventoryId = inventoryId;
        return ReadException is null
            ? Task.FromResult(new GetStockReservationsResult(Items, pageNumber, pageSize, TotalCount))
            : Task.FromException<GetStockReservationsResult>(ReadException);
    }

    public StockReservationDto? Result { get; set; }
    public Exception? ReadException { get; set; }
    public int CallCount { get; private set; }
    public Guid? ReceivedId { get; private set; }

    public Task<StockReservationDto?> GetByIdAsync(Guid reservationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        ReceivedId = reservationId;
        if (ReadException is not null) return Task.FromException<StockReservationDto?>(ReadException);
        return Task.FromResult(Result?.ReservationId == reservationId ? Result : null);
    }
}
