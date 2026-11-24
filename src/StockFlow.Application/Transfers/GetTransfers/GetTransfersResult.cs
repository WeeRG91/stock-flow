namespace StockFlow.Application.Transfers.GetTransfers
{
    public sealed record GetTransfersResult(
        IReadOnlyList<TransferSummaryDto> Items,
        int PageNumber,
        int PageSize,
        int TotalCount);
}
