using StockFlow.Application.Exceptions;
using StockFlow.Application.Suppliers;
using StockFlow.Application.Suppliers.GetSupplierById;
using StockFlow.Application.Suppliers.GetSuppliers;

namespace StockFlow.Application.Tests;

public class GetSupplierByIdQueryHandlerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_ReturnsSupplierAndForwardsArguments(
        bool isActive)
    {
        var supplier = new SupplierDto(
            Guid.NewGuid(), "DEMO-OFFICE", "[DEMO] Office Supplies",
            "office@example.com", null, isActive);

        var reader = new FakeReader { Result = supplier };
        var handler = new GetSupplierByIdQueryHandler(reader);
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(
            new GetSupplierByIdQuery(supplier.Id),
            cancellation.Token);

        Assert.Same(supplier, result);
        Assert.Equal(supplier.Id, reader.ReceivedId);
        Assert.Equal(cancellation.Token, reader.ReceivedToken);
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public void Constructor_WithNullReader_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GetSupplierByIdQueryHandler(null!));
    }

    [Fact]
    public async Task HandleAsync_WithNullQuery_ThrowsWithoutReading()
    {
        var reader = new FakeReader();
        var handler = new GetSupplierByIdQueryHandler(reader);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.HandleAsync(null!));

        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyId_ThrowsValidation()
    {
        var reader = new FakeReader();
        var handler = new GetSupplierByIdQueryHandler(reader);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => handler.HandleAsync(
                new GetSupplierByIdQuery(Guid.Empty)));

        Assert.Equal("SupplierId", exception.FieldName);
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenMissing_ThrowsNotFound()
    {
        var reader = new FakeReader();
        var handler = new GetSupplierByIdQueryHandler(reader);
        var id = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => handler.HandleAsync(new GetSupplierByIdQuery(id)));

        Assert.Equal("Supplier", exception.ResourceName);
        Assert.Equal(id, exception.ResourceId);
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenCancelled_DoesNotRead()
    {
        var reader = new FakeReader();
        var handler = new GetSupplierByIdQueryHandler(reader);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(
                new GetSupplierByIdQuery(Guid.NewGuid()),
                cancellation.Token));

        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenReaderFails_PropagatesFailure()
    {
        var failure = new InvalidOperationException("Simulated failure");
        var reader = new FakeReader { Failure = failure };
        var handler = new GetSupplierByIdQueryHandler(reader);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                new GetSupplierByIdQuery(Guid.NewGuid())));

        Assert.Same(failure, exception);
        Assert.Equal(1, reader.CallCount);
    }

    private sealed class FakeReader : ISupplierReadService
    {
        public SupplierDto? Result { get; set; }
        public Exception? Failure { get; set; }
        public int CallCount { get; private set; }
        public Guid ReceivedId { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }

        public Task<SupplierDto?> GetSupplierByIdAsync(
            Guid supplierId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ReceivedId = supplierId;
            ReceivedToken = cancellationToken;

            return Failure is null
                ? Task.FromResult(Result)
                : Task.FromException<SupplierDto?>(Failure);
        }

        public Task<GetSuppliersResult> GetSuppliersAsync(
            int pageNumber,
            int pageSize,
            string? search,
            bool? isActive,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "List lookups are not used by these details tests.");
        }
    }
}