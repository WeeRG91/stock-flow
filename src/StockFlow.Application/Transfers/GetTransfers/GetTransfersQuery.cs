using StockFlow.Domain.Enums;

namespace StockFlow.Application.Transfers.GetTransfers
{
    public sealed record GetTransfersQuery(
    int PageNumber = 1,
    int PageSize = 20,
    TransferStatus? Status = null,
    string? Search = null);
}
