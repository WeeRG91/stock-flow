using Microsoft.AspNetCore.SignalR;
using StockFlow.API.Hubs;
using StockFlow.Application.Abstractions.Notifications;

namespace StockFlow.API.Notifications
{
    public sealed class SignalRTransferChangeNotifier : ITransferChangeNotifier
    {
        private readonly IHubContext<TransferHub> _hubContext;
        private readonly ILogger<SignalRTransferChangeNotifier> _logger;

        public SignalRTransferChangeNotifier(
        IHubContext<TransferHub> hubContext,
        ILogger<SignalRTransferChangeNotifier> logger)
        {
            ArgumentNullException.ThrowIfNull(hubContext);
            ArgumentNullException.ThrowIfNull(logger);

            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyAsync(
            TransferChanged notification,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(notification);

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                timeout.CancelAfter(TimeSpan.FromSeconds(3));

                await _hubContext.Clients.All.SendAsync(
                    "TransferChanged",
                    notification,
                    timeout.Token);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                exception,
                "Transfer {TransferId} was committed, but its change notification failed.",
                notification.TransferId);
            }
        }
    }
}
