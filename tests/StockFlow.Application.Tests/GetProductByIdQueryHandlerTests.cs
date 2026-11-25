using StockFlow.Application.Exceptions;
using StockFlow.Application.Products.GetProductById;
using StockFlow.Application.Products.GetProducts;

namespace StockFlow.Application.Tests;

public class GetProductByIdQueryHandlerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_ReturnsProductAndForwardsArguments(bool isActive)
    {
        var product = new ProductDto(
            Guid.NewGuid(), "Synthetic product", "TEST-PRODUCT", null, isActive);
        var reader = new FakeReader { Result = product };
        var handler = new GetProductByIdQueryHandler(reader);
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(
            new GetProductByIdQuery(product.Id), cancellation.Token);

        Assert.Same(product, result);
        Assert.Equal(product.Id, reader.ReceivedId);
        Assert.Equal(cancellation.Token, reader.ReceivedToken);
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public void Constructor_WithNullReader_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GetProductByIdQueryHandler(null!));
    }

    [Fact]
    public async Task HandleAsync_WithNullQuery_ThrowsWithoutReading()
    {
        var reader = new FakeReader();
        var handler = new GetProductByIdQueryHandler(reader);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.HandleAsync(null!));

        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyId_ThrowsValidationWithoutReading()
    {
        var reader = new FakeReader();
        var handler = new GetProductByIdQueryHandler(reader);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => handler.HandleAsync(new GetProductByIdQuery(Guid.Empty)));

        Assert.Equal("ProductId", exception.FieldName);
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenMissing_ThrowsNotFound()
    {
        var reader = new FakeReader();
        var handler = new GetProductByIdQueryHandler(reader);
        var id = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => handler.HandleAsync(new GetProductByIdQuery(id)));

        Assert.Equal("Product", exception.ResourceName);
        Assert.Equal(id, exception.ResourceId);
        Assert.Equal(id, reader.ReceivedId);
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenAlreadyCancelled_DoesNotRead()
    {
        var reader = new FakeReader();
        var handler = new GetProductByIdQueryHandler(reader);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(
                new GetProductByIdQuery(Guid.NewGuid()), cancellation.Token));

        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenReaderFails_PropagatesFailure()
    {
        var failure = new InvalidOperationException("Simulated read failure");
        var reader = new FakeReader { Failure = failure };
        var handler = new GetProductByIdQueryHandler(reader);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(new GetProductByIdQuery(Guid.NewGuid())));

        Assert.Same(failure, exception);
        Assert.Equal(1, reader.CallCount);
    }

    private sealed class FakeReader : IProductReadService
    {
        public ProductDto? Result { get; set; }
        public Exception? Failure { get; set; }
        public int CallCount { get; private set; }
        public Guid ReceivedId { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }

        public Task<ProductDto?> GetByIdAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ReceivedId = productId;
            ReceivedToken = cancellationToken;

            return Failure is null
                ? Task.FromResult(Result)
                : Task.FromException<ProductDto?>(Failure);
        }

        public Task<IReadOnlyList<ProductDto>> GetProductsAsync(
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "List lookups are not used by these detail-query tests.");
        }
    }
}
