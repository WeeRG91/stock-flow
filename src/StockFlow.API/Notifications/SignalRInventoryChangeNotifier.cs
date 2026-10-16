using Microsoft.AspNetCore.SignalR;
using StockFlow.API.Hubs;
using StockFlow.Application.Abstractions.Notifications;

namespace StockFlow.API.Notifications
{
    public sealed class SignalRInventoryChangeNotifier : IInventoryChangeNotifier
    {
        private readonly IHubContext<InventoryHub> _hubContext;
        private readonly ILogger<SignalRInventoryChangeNotifier> _logger;

        public SignalRInventoryChangeNotifier(
            IHubContext<InventoryHub> hubContext,
            ILogger<SignalRInventoryChangeNotifier> logger)
        {
            ArgumentNullException.ThrowIfNull(hubContext);
            ArgumentNullException.ThrowIfNull(logger);

            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyAsync(InventoryChanged notification, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(notification);

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                timeout.CancelAfter(TimeSpan.FromSeconds(3));

                await _hubContext.Clients.All.SendAsync(
                    "InventoryChanged",
                    notification,
                    timeout.Token);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Inventory was committed, but its change notification failed.");
            }
        }
    }
}
