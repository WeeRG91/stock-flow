using StockFlow.Application.Exceptions;
using StockFlow.Application.Inventory.GetInventory;

namespace StockFlow.Application.Tests
{
    public class GetInventoryQueryHandlerTests
    {
        [Theory]
        [InlineData(1, 20)]
        [InlineData(2, 100)]
        public async Task HandleAsync_WithValidPagination_ForwardsParameters(
            int pageNumber,
            int pageSize)
        {
            var reader = new FakeInventoryReadService();
            var handler = new GetInventoryQueryHandler(reader);
            using var cancellation = new CancellationTokenSource();

            var result = await handler.HandleAsync(
                new GetInventoryQuery(pageNumber, pageSize),
                cancellation.Token);

            Assert.Equal(pageNumber, reader.ReceivedPageNumber);
            Assert.Equal(pageSize, reader.ReceivedPageSize);
            Assert.Equal(cancellation.Token, reader.ReceivedToken);
            Assert.Equal(1, reader.CallCount);

            Assert.Equal(pageNumber, result.PageNumber);
            Assert.Equal(pageSize, result.PageSize);
            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalCount);
        }

        [Theory]
        [InlineData(0, 20, "PageNumber")]
        [InlineData(-1, 20, "PageNumber")]
        [InlineData(1, 0, "PageSize")]
        [InlineData(1, -1, "PageSize")]
        [InlineData(1, 101, "PageSize")]
        [InlineData(int.MaxValue, 100, "PageNumber")]
        public void HandleAsync_WithInvalidPagination_RejectsBeforeReading(
            int pageNumber,
            int pageSize,
            string expectedField)
        {
            var reader = new FakeInventoryReadService();
            var handler = new GetInventoryQueryHandler(reader);

            var exception = Assert.Throws<RequestValidationException>(() =>
            {
                _ = handler.HandleAsync(
                    new GetInventoryQuery(pageNumber, pageSize));
            });

            Assert.Equal(expectedField, exception.FieldName);
            Assert.Equal(0, reader.CallCount);
        }

        [Fact]
        public void HandleAsync_WithNullQuery_RejectsBeforeReading()
        {
            var reader = new FakeInventoryReadService();
            var handler = new GetInventoryQueryHandler(reader);

            Assert.Throws<ArgumentNullException>(() =>
            {
                _ = handler.HandleAsync(null!);
            });

            Assert.Equal(0, reader.CallCount);
        }

        [Fact]
        public void HandleAsync_WhenCancelled_DoesNotRead()
        {
            var reader = new FakeInventoryReadService();
            var handler = new GetInventoryQueryHandler(reader);

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
            {
                _ = handler.HandleAsync(
                    new GetInventoryQuery(),
                    cancellation.Token);
            });

            Assert.Equal(0, reader.CallCount);
        }

        private sealed class FakeInventoryReadService : IInventoryReadService
        {
            public int CallCount { get; private set; }
            public int ReceivedPageNumber { get; private set; }
            public int ReceivedPageSize { get; private set; }
            public CancellationToken ReceivedToken { get; private set; }

            public Task<GetInventoryResult> GetInventoryAsync(
                int pageNumber,
                int pageSize,
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                ReceivedPageNumber = pageNumber;
                ReceivedPageSize = pageSize;
                ReceivedToken = cancellationToken;

                return Task.FromResult(new GetInventoryResult(
                    Array.Empty<InventoryBalanceDto>(),
                    pageNumber,
                    pageSize,
                    0));
            }
        }
    }
}