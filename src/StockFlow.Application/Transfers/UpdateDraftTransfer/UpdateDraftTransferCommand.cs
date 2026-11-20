namespace StockFlow.Application.Transfers.UpdateDraftTransfer
{
    public sealed record UpdateDraftTransferCommand(
        Guid TransferId,
        Guid ExpectedVersion,
        IReadOnlyList<UpdateDraftTransferLineCommand> Lines);

    public sealed record UpdateDraftTransferLineCommand(
        Guid ProductId,
        int Quantity);
}
