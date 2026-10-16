using StockFlow.Application.Warehouses.GetWarehouses;

namespace StockFlow.Application.Tests
{
    public class GetWarehousesQueryHandlerTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task HandleAsync_ReturnsReadResultsAndForwardsCancellation(
            bool includeWarehouse)
        {
            IReadOnlyList<WarehouseDto> warehouses = includeWarehouse
                ? new[]
                {
                    new WarehouseDto(
                        Guid.NewGuid(),
                        "TEST-BRU",
                        "Synthetic Brussels warehouse",
                        "Brussels",
                        false)
                }
                : Array.Empty<WarehouseDto>();

            var reader = new FakeWarehouseReadService
            {
                Warehouses = warehouses
            };

            var handler = new GetWarehousesQueryHandler(reader);
            using var cancellation = new CancellationTokenSource();

            var result = await handler.HandleAsync(
                new GetWarehousesQuery(),
                cancellation.Token);

            Assert.Same(warehouses, result);
            Assert.Equal(1, reader.CallCount);
            Assert.Equal(cancellation.Token, reader.ReceivedToken);
        }

        [Fact]
        public void HandleAsync_WithNullQuery_RejectsBeforeReading()
        {
            var reader = new FakeWarehouseReadService();
            var handler = new GetWarehousesQueryHandler(reader);

            Assert.Throws<ArgumentNullException>(() =>
            {
                _ = handler.HandleAsync(null!);
            });

            Assert.Equal(0, reader.CallCount);
        }

        [Fact]
        public void HandleAsync_WhenAlreadyCancelled_DoesNotRead()
        {
            var reader = new FakeWarehouseReadService();
            var handler = new GetWarehousesQueryHandler(reader);

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
            {
                _ = handler.HandleAsync(
                    new GetWarehousesQuery(),
                    cancellation.Token);
            });

            Assert.Equal(0, reader.CallCount);
        }

        private sealed class FakeWarehouseReadService : IWarehouseReadService
        {
            public IReadOnlyList<WarehouseDto> Warehouses { get; set; } =
                Array.Empty<WarehouseDto>();

            public int CallCount { get; private set; }
            public CancellationToken ReceivedToken { get; private set; }

            public Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                ReceivedToken = cancellationToken;

                return Task.FromResult(Warehouses);
            }
        }
    }
}