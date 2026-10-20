using StockFlow.Application.Exceptions;
using StockFlow.Application.Suppliers;
using StockFlow.Application.Suppliers.GetSuppliers;

namespace StockFlow.Application.Tests;

public class GetSuppliersQueryHandlerTests
{
    [Theory]
    [InlineData(1, 20)]
    [InlineData(2, 100)]
    public async Task HandleAsync_ForwardsPaginationAndReturnsReaderResult(
        int pageNumber,
        int pageSize)
    {
        var expected = new GetSuppliersResult(
            new[]
            {
                new SupplierDto(
                    Guid.NewGuid(),
                    "SUP-001",
                    "Supplier",
                    "sales@example.com",
                    null,
                    false)
            },
            pageNumber,
            pageSize,
            7);

        var reader = new FakeSupplierReadService { Result = expected };
        var handler = new GetSuppliersQueryHandler(reader);
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(
            new GetSuppliersQuery(pageNumber, pageSize),
            cancellation.Token);

        Assert.Same(expected, result);
        Assert.Equal(pageNumber, reader.ReceivedPageNumber);
        Assert.Equal(pageSize, reader.ReceivedPageSize);
        Assert.Equal(cancellation.Token, reader.ReceivedToken);
        Assert.Equal(1, reader.CallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_PreservesStatusFilter(bool? isActive)
    {
        var reader = new FakeSupplierReadService();
        var handler = new GetSuppliersQueryHandler(reader);

        await handler.HandleAsync(
            new GetSuppliersQuery(IsActive: isActive));

        Assert.Equal(isActive, reader.ReceivedIsActive);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(" \t ", null)]
    [InlineData("  office  ", "office")]
    [InlineData(" %_\\ ", "%_\\")]
    public async Task HandleAsync_NormalizesSearch(
        string? search,
        string? expected)
    {
        var reader = new FakeSupplierReadService();
        var handler = new GetSuppliersQueryHandler(reader);

        await handler.HandleAsync(
            new GetSuppliersQuery(Search: search));

        Assert.Equal(expected, reader.ReceivedSearch);
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
        var reader = new FakeSupplierReadService();
        var handler = new GetSuppliersQueryHandler(reader);

        var exception = Assert.Throws<RequestValidationException>(() =>
        {
            _ = handler.HandleAsync(
                new GetSuppliersQuery(pageNumber, pageSize));
        });

        Assert.Equal(expectedField, exception.FieldName);
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public void Constructor_WithNullReader_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GetSuppliersQueryHandler(null!));
    }

    [Fact]
    public void HandleAsync_WithNullQuery_DoesNotRead()
    {
        var reader = new FakeSupplierReadService();
        var handler = new GetSuppliersQueryHandler(reader);

        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = handler.HandleAsync(null!);
        });

        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public void HandleAsync_WhenAlreadyCancelled_DoesNotRead()
    {
        var reader = new FakeSupplierReadService();
        var handler = new GetSuppliersQueryHandler(reader);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
        {
            _ = handler.HandleAsync(
                new GetSuppliersQuery(), cancellation.Token);
        });

        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenReaderFails_PropagatesFailure()
    {
        var failure = new InvalidOperationException("Simulated read failure.");
        var reader = new FakeSupplierReadService
        {
            ReadException = failure
        };
        var handler = new GetSuppliersQueryHandler(reader);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(new GetSuppliersQuery()));

        Assert.Same(failure, exception);
    }

    private sealed class FakeSupplierReadService : ISupplierReadService
    {
        public Task<SupplierDto?> GetSupplierByIdAsync(
            Guid supplierId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "Supplier details lookups are not used by these list-query tests.");
        }

        public GetSuppliersResult Result { get; set; } =
            new(Array.Empty<SupplierDto>(), 1, 20, 0);

        public Exception? ReadException { get; set; }
        public int CallCount { get; private set; }
        public int ReceivedPageNumber { get; private set; }
        public int ReceivedPageSize { get; private set; }
        public string? ReceivedSearch { get; private set; }
        public bool? ReceivedIsActive { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }

        public Task<GetSuppliersResult> GetSuppliersAsync(
            int pageNumber,
            int pageSize,
            string? search,
            bool? isActive,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ReceivedPageNumber = pageNumber;
            ReceivedPageSize = pageSize;
            ReceivedSearch = search;
            ReceivedIsActive = isActive;
            ReceivedToken = cancellationToken;

            if (ReadException is not null)
            {
                return Task.FromException<GetSuppliersResult>(
                    ReadException);
            }

            return Task.FromResult(Result);
        }
    }
}
