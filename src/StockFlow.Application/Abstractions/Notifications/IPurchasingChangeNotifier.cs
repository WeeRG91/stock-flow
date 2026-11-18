namespace StockFlow.Application.Abstractions.Notifications
{
    public interface IPurchasingChangeNotifier
    {
        /// <summary>
        /// Attempts to refresh purchasing views and the specified users'
        /// inboxes after a successful commit. Delivery failures must not
        /// fail the committed operation.
        /// </summary>
        Task NotifyAsync(
            IReadOnlyCollection<Guid> inboxRecipientIds,
            CancellationToken cancellationToken = default);
    }
}
