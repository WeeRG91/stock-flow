using StockFlow.Domain.Enums;

namespace StockFlow.Application.Transfers.GetTransfers
{
    public sealed record TransferSummaryDto(
        Guid Id,
        string SourceWarehouseCode,
        string SourceWarehouseName,
        string DestinationWarehouseCode,
        string DestinationWarehouseName,
        TransferStatus Status,
        DateTimeOffset CreatedAt,
        int LineCount)
    {
        public DateTimeOffset? DispatchedAt { get; init; }
    }
}
