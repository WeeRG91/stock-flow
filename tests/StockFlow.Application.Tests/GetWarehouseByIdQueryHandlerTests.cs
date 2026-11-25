using StockFlow.Application.Exceptions;
using StockFlow.Application.Warehouses.GetWarehouseById;
using StockFlow.Application.Warehouses.GetWarehouses;

namespace StockFlow.Application.Tests;

public class GetWarehouseByIdQueryHandlerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_ReturnsWarehouseAndForwardsArguments(bool isActive)
    {
        var warehouse = new WarehouseDto(
            Guid.NewGuid(), "TEST-BRU", "Synthetic warehouse", "Brussels", isActive);
        var reader = new FakeReader { Result = warehouse };
        var handler = new GetWarehouseByIdQueryHandler(reader);
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(
            new GetWarehouseByIdQuery(warehouse.Id), cancellation.Token);

        Assert.Same(warehouse, result);
        Assert.Equal(warehouse.Id, reader.ReceivedId);
        Assert.Equal(cancellation.Token, reader.ReceivedToken);
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public void Constructor_WithNullReader_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GetWarehouseByIdQueryHandler(null!));
    }

    [Fact]
    public async Task HandleAsync_WithNullQuery_ThrowsWithoutReading()
    {
        var reader = new FakeReader();
        var handler = new GetWarehouseByIdQueryHandler(reader);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.HandleAsync(null!));

        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyId_ThrowsValidationWithoutReading()
    {
        var reader = new FakeReader();
        var handler = new GetWarehouseByIdQueryHandler(reader);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => handler.HandleAsync(new GetWarehouseByIdQuery(Guid.Empty)));

        Assert.Equal("WarehouseId", exception.FieldName);
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenMissing_ThrowsNotFound()
    {
        var reader = new FakeReader();
        var handler = new GetWarehouseByIdQueryHandler(reader);
        var id = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => handler.HandleAsync(new GetWarehouseByIdQuery(id)));

        Assert.Equal("Warehouse", exception.ResourceName);
        Assert.Equal(id, exception.ResourceId);
        Assert.Equal(id, reader.ReceivedId);
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenAlreadyCancelled_DoesNotRead()
    {
        var reader = new FakeReader();
        var handler = new GetWarehouseByIdQueryHandler(reader);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(
                new GetWarehouseByIdQuery(Guid.NewGuid()), cancellation.Token));

        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenReaderFails_PropagatesFailure()
    {
        var failure = new InvalidOperationException("Simulated read failure");
        var reader = new FakeReader { Failure = failure };
        var handler = new GetWarehouseByIdQueryHandler(reader);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(new GetWarehouseByIdQuery(Guid.NewGuid())));

        Assert.Same(failure, exception);
        Assert.Equal(1, reader.CallCount);
    }

    private sealed class FakeReader : IWarehouseReadService
    {
        public WarehouseDto? Result { get; set; }
        public Exception? Failure { get; set; }
        public int CallCount { get; private set; }
        public Guid ReceivedId { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }

        public Task<WarehouseDto?> GetByIdAsync(
            Guid warehouseId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ReceivedId = warehouseId;
            ReceivedToken = cancellationToken;

            return Failure is null
                ? Task.FromResult(Result)
                : Task.FromException<WarehouseDto?>(Failure);
        }

        public Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "List lookups are not used by these detail-query tests.");
        }
    }
}
