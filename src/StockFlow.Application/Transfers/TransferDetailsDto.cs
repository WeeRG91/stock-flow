using StockFlow.Domain.Enums;

namespace StockFlow.Application.Transfers
{
    public sealed record TransferDetailsDto(
        Guid Id,
        Guid SourceWarehouseId,
        string SourceWarehouseCode,
        string SourceWarehouseName,
        Guid DestinationWarehouseId,
        string DestinationWarehouseCode,
        string DestinationWarehouseName,
        TransferStatus Status,
        DateTimeOffset CreatedAt,
        Guid CreatedByUserId,
        DateTimeOffset? DispatchedAt,
        Guid? DispatchedByUserId,
        DateTimeOffset? CancelledAt,
        Guid? CancelledByUserId,
        DateTimeOffset? ReceivedAt,
        Guid Version,
        IReadOnlyList<TransferLineDto> Lines);
}
