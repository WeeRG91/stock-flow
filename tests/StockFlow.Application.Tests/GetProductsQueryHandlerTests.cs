using StockFlow.Application.Products.GetProducts;

namespace StockFlow.Application.Tests
{
    public class GetProductsQueryHandlerTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task HandleAsync_ReturnsReadResultsAndForwardsCancellation(
            bool includeProduct)
        {
            // Arrange
            IReadOnlyList<ProductDto> products = includeProduct
                ? new[]
                {
                    new ProductDto(
                        Guid.NewGuid(),
                        "Synthetic test product",
                        "TEST-PRODUCT",
                        null,
                        false)
                }
                : Array.Empty<ProductDto>();

            var reader = new FakeProductReadService
            {
                Products = products
            };

            var handler = new GetProductsQueryHandler(reader);
            using var cancellation = new CancellationTokenSource();

            // Act
            var result = await handler.HandleAsync(
                new GetProductsQuery(),
                cancellation.Token);

            // Assert
            Assert.Same(products, result);
            Assert.Equal(1, reader.CallCount);
            Assert.Equal(cancellation.Token, reader.ReceivedToken);
        }

        [Fact]
        public void HandleAsync_WithNullQuery_RejectsBeforeReading()
        {
            var reader = new FakeProductReadService();
            var handler = new GetProductsQueryHandler(reader);

            Assert.Throws<ArgumentNullException>(() =>
            {
                _ = handler.HandleAsync(null!);
            });

            Assert.Equal(0, reader.CallCount);
        }

        [Fact]
        public void HandleAsync_WhenAlreadyCancelled_DoesNotRead()
        {
            var reader = new FakeProductReadService();
            var handler = new GetProductsQueryHandler(reader);

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
            {
                _ = handler.HandleAsync(
                    new GetProductsQuery(),
                    cancellation.Token);
            });

            Assert.Equal(0, reader.CallCount);
        }

        private sealed class FakeProductReadService : IProductReadService
        {
            public IReadOnlyList<ProductDto> Products { get; set; } =
                Array.Empty<ProductDto>();

            public int CallCount { get; private set; }
            public CancellationToken ReceivedToken { get; private set; }

            public Task<IReadOnlyList<ProductDto>> GetProductsAsync(
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                ReceivedToken = cancellationToken;

                return Task.FromResult(Products);
            }
        }
    }
}