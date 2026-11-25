using Microsoft.Extensions.Logging.Abstractions;
using StockFlow.API.Notifications;
using StockFlow.API.Tests.Fakes;
using StockFlow.Application.Abstractions.Notifications;

namespace StockFlow.API.Tests;

public sealed class SignalRTransferChangeNotifierTests
{
    [Fact]
    public async Task NotifyAsync_BroadcastsExpectedEventAndTransferPayload()
    {
        var client = new TestInventoryClientProxy();
        var notification = new TransferChanged(Guid.NewGuid());

        await CreateNotifier(client).NotifyAsync(notification);

        Assert.Equal(1, client.SendCallCount);
        Assert.Equal("TransferChanged", client.LastMethod);
        Assert.NotNull(client.LastArguments);
        Assert.Same(notification, Assert.Single(client.LastArguments));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NotifyAsync_WhenDeliveryFails_DoesNotThrow(bool cancelled)
    {
        var client = new TestInventoryClientProxy
        {
            SendException = cancelled
                ? new OperationCanceledException("Simulated send cancellation.")
                : new InvalidOperationException("Simulated transport failure.")
        };

        var error = await Record.ExceptionAsync(() =>
            CreateNotifier(client).NotifyAsync(new TransferChanged(Guid.NewGuid())));

        Assert.Null(error);
        Assert.Equal(1, client.SendCallCount);
    }

    [Fact]
    public async Task NotifyAsync_WhenTokenAlreadyCancelled_DoesNotThrow()
    {
        var client = new TestInventoryClientProxy();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var error = await Record.ExceptionAsync(() => CreateNotifier(client).NotifyAsync(
            new TransferChanged(Guid.NewGuid()), cancellation.Token));

        Assert.Null(error);
        Assert.Equal(1, client.SendCallCount);
    }

    private static SignalRTransferChangeNotifier CreateNotifier(TestInventoryClientProxy client) =>
        new(new TestTransferHubContext(client), NullLogger<SignalRTransferChangeNotifier>.Instance);
}
