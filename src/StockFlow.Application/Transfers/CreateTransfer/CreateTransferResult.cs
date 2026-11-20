using StockFlow.Domain.Enums;

namespace StockFlow.Application.Transfers.CreateTransfer
{
    public sealed record CreateTransferResult(
        Guid TransferId,
        TransferStatus Status,
        DateTimeOffset CreatedAt,
        Guid CreatedByUserId);
}
