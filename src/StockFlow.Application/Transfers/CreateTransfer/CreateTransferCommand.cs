namespace StockFlow.Application.Transfers.CreateTransfer
{
    public sealed record CreateTransferCommand(
        Guid SourceWarehouseId,
        Guid DestinationWarehouseId,
        IReadOnlyList<CreateTransferLineCommand> Lines);

    public sealed record CreateTransferLineCommand(
        Guid ProductId,
        int Quantity);
}
