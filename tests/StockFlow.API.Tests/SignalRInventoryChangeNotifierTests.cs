using Microsoft.Extensions.Logging.Abstractions;
using StockFlow.API.Notifications;
using StockFlow.API.Tests.Fakes;
using StockFlow.Application.Abstractions.Notifications;

namespace StockFlow.API.Tests
{
    public sealed class SignalRInventoryChangeNotifierTests
    {
        private static SignalRInventoryChangeNotifier CreateNotifier(
            TestInventoryClientProxy client) => new(
                new TestInventoryHubContext(client),
                NullLogger<SignalRInventoryChangeNotifier>.Instance);

        [Fact]
        public async Task NotifyAsync_BroadcastsExpectedEventAndPayload()
        {
            var client = new TestInventoryClientProxy();
            var notification = new InventoryChanged();
            await CreateNotifier(client).NotifyAsync(notification);
            Assert.Equal(1, client.SendCallCount);
            Assert.Equal("InventoryChanged", client.LastMethod);
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
            var exception = await Record.ExceptionAsync(
                () => CreateNotifier(client).NotifyAsync(new InventoryChanged()));
            Assert.Null(exception);
            Assert.Equal(1, client.SendCallCount);
        }

        [Fact]
        public async Task NotifyAsync_WhenTokenAlreadyCancelled_DoesNotThrow()
        {
            var client = new TestInventoryClientProxy();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var exception = await Record.ExceptionAsync(() =>
                CreateNotifier(client).NotifyAsync(new InventoryChanged(), cancellation.Token));
            Assert.Null(exception);
        }
    }
}
