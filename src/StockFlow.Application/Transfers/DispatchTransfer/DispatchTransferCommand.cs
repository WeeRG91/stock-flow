namespace StockFlow.Application.Transfers.DispatchTransfer
{
    public sealed record DispatchTransferCommand(
        Guid TransferId,
        Guid ExpectedVersion);
}
