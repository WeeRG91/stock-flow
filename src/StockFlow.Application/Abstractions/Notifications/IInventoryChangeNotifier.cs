namespace StockFlow.Application.Abstractions.Notifications
{
    public interface IInventoryChangeNotifier
    {
        /// <summary>
        /// Attempts notification after successful persistence.
        /// Delivery failures must not fail the committed operation.
        /// </summary>
        Task NotifyAsync(
            InventoryChanged notification,
            CancellationToken cancellationToken = default);
    }
}
