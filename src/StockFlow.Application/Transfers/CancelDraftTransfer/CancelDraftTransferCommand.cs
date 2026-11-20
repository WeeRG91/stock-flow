namespace StockFlow.Application.Transfers.CancelDraftTransfer
{
    public sealed record CancelDraftTransferCommand(
        Guid TransferId,
        Guid ExpectedVersion);
}
