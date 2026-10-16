using StockFlow.Application.Abstractions.Notifications;

namespace StockFlow.Application.Tests.Fakes
{
    public sealed class FakeInventoryChangeNotifier
        : IInventoryChangeNotifier
    {
        private readonly List<InventoryChanged> _notifications = [];

        public IReadOnlyList<InventoryChanged> Notifications =>
            _notifications.AsReadOnly();

        public CancellationToken LastCancellationToken { get; private set; }

        public Task NotifyAsync(
            InventoryChanged notification,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(notification);

            LastCancellationToken = cancellationToken;
            _notifications.Add(notification);

            return Task.CompletedTask;
        }
    }
}