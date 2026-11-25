using StockFlow.Application.Abstractions.Notifications;

namespace StockFlow.Application.Tests.Fakes;

internal sealed class FakeTransferChangeNotifier : ITransferChangeNotifier
{
    public List<TransferChanged> Notifications { get; } = [];
    public CancellationToken LastToken { get; private set; }
    public Action<TransferChanged>? OnNotify { get; set; }

    public Task NotifyAsync(
        TransferChanged notification,
        CancellationToken cancellationToken = default)
    {
        OnNotify?.Invoke(notification);
        Notifications.Add(notification);
        LastToken = cancellationToken;
        return Task.CompletedTask;
    }
}
