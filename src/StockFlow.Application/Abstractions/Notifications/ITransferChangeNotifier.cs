namespace StockFlow.Application.Abstractions.Notifications
{
    public interface ITransferChangeNotifier
    {
        Task NotifyAsync(
            TransferChanged notification,
            CancellationToken cancellationToken = default);
    }
}
