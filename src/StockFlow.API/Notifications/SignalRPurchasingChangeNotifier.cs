using Microsoft.AspNetCore.SignalR;
using StockFlow.API.Hubs;
using StockFlow.Application.Abstractions.Notifications;

namespace StockFlow.API.Notifications
{
    public sealed class SignalRPurchasingChangeNotifier(
        IHubContext<PurchasingHub> hubContext,
        ILogger<SignalRPurchasingChangeNotifier> logger)
        : IPurchasingChangeNotifier
    {
        public async Task NotifyAsync(
            IReadOnlyCollection<Guid> inboxRecipientIds,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(3));

                var purchasingRefresh = hubContext.Clients.All.SendAsync(
                    "PurchasingChanged", cancellationToken: timeout.Token);
                if (inboxRecipientIds.Count == 0)
                {
                    await purchasingRefresh;
                    return;
                }

                var userIds = inboxRecipientIds
                    .Select(id => id.ToString("D"))
                    .Distinct()
                    .ToArray();

                var inboxRefresh = hubContext.Clients.Users(userIds).SendAsync(
                    "PurchasingNotificationsChanged", cancellationToken: timeout.Token);

                await Task.WhenAll(purchasingRefresh, inboxRefresh);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "The purchasing change was committed, but its live refresh failed.");
            }
        }
    }
}
