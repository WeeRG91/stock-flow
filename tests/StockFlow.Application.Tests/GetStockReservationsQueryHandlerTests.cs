using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Application.StockReservations;
using StockFlow.Application.StockReservations.GetStockReservations;
using StockFlow.Application.Tests.Fakes;
using StockFlow.Domain.Enums;

namespace StockFlow.Application.Tests;

public sealed class GetStockReservationsQueryHandlerTests
{
    [Theory]
    [InlineData(null, null, null)]
    [InlineData(1, "", null)]
    [InlineData(2, " \t ", null)]
    [InlineData(3, "  Operation A  ", "Operation A")]
    [InlineData(null, "%_\\", "%_\\")]
    public async Task GetPage_ForwardsFiltersAndNormalizesSearch(
        int? statusValue, string? search, string? expectedSearch)
    {
        var f = new Fixture();
        var inventoryId = Guid.NewGuid();
        var status = statusValue.HasValue ? (StockReservationStatus?)statusValue.Value : null;
        using var cancellation = new CancellationTokenSource();
        var result = await f.Handler.HandleAsync(new(2, 10, status, search, inventoryId), cancellation.Token);
        Assert.Same(f.Reader.Result, result);
        Assert.Equal(2, f.Reader.PageNumber);
        Assert.Equal(10, f.Reader.PageSize);
        Assert.Equal(status, f.Reader.Status);
        Assert.Equal(expectedSearch, f.Reader.Search);
        Assert.Equal(inventoryId, f.Reader.InventoryId);
        Assert.Equal(cancellation.Token, f.Reader.Token);
        Assert.Equal(1, f.Reader.CallCount);
    }

    [Fact]
    public async Task GetPage_WithDefaults_ForwardsUnfilteredFirstPage()
    {
        var f = new Fixture();
        await f.Handler.HandleAsync(new());
        Assert.Equal(1, f.Reader.PageNumber);
        Assert.Equal(20, f.Reader.PageSize);
        Assert.Null(f.Reader.InventoryId);
        Assert.Null(f.Reader.Status);
        Assert.Null(f.Reader.Search);
    }

    [Theory]
    [InlineData(0, 20, "PageNumber")]
    [InlineData(-1, 20, "PageNumber")]
    [InlineData(1, 0, "PageSize")]
    [InlineData(1, -1, "PageSize")]
    [InlineData(1, 101, "PageSize")]
    [InlineData(int.MaxValue, 100, "PageNumber")]
    public async Task GetPage_WithInvalidPagination_RejectsBeforeReading(int page, int size, string field)
    {
        var f = new Fixture();
        var error = await Assert.ThrowsAsync<RequestValidationException>(() => f.Handler.HandleAsync(new(page, size)));
        Assert.Equal(field, error.FieldName);
        Assert.Equal(0, f.Reader.CallCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public async Task GetPage_WithUndefinedStatus_RejectsBeforeReading(int status)
    {
        var f = new Fixture();
        var error = await Assert.ThrowsAsync<RequestValidationException>(() =>
            f.Handler.HandleAsync(new(Status: (StockReservationStatus)status)));
        Assert.Equal("Status", error.FieldName);
        Assert.Equal(0, f.Reader.CallCount);
    }

    [Fact]
    public async Task GetPage_WithEmptyInventoryId_RejectsBeforeReading()
    {
        var f = new Fixture();
        var error = await Assert.ThrowsAsync<RequestValidationException>(() =>
            f.Handler.HandleAsync(new(InventoryId: Guid.Empty)));
        Assert.Equal("InventoryId", error.FieldName);
        Assert.Equal(0, f.Reader.CallCount);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("emptyUser")]
    [InlineData("permission")]
    public async Task GetPage_WithoutAccess_RejectsBeforeReading(string scenario)
    {
        var f = new Fixture();
        if (scenario == "anonymous") f.User.UserId = null;
        if (scenario == "emptyUser") f.User.UserId = Guid.Empty;
        if (scenario == "permission")
        {
            f.User.Permissions.Clear();
            f.User.Permissions.Add(StockFlowPolicies.StockReservationsManage);
        }
        if (scenario == "permission")
            await Assert.ThrowsAsync<PermissionDeniedException>(() => f.Handler.HandleAsync(new()));
        else
            await Assert.ThrowsAsync<AuthenticationRequiredException>(() => f.Handler.HandleAsync(new()));
        Assert.Equal(0, f.Reader.CallCount);
    }

    [Fact]
    public async Task GetPage_WithNullQuery_RejectsBeforeReading()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ArgumentNullException>(() => f.Handler.HandleAsync(null!));
        Assert.Equal(0, f.Reader.CallCount);
    }

    [Fact]
    public async Task GetPage_WhenCancelled_DoesNotRead()
    {
        var f = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Handler.HandleAsync(new(), cancellation.Token));
        Assert.Equal(0, f.Reader.CallCount);
    }

    [Fact]
    public async Task GetPage_WhenReaderFails_PropagatesFailure()
    {
        var f = new Fixture();
        var failure = new InvalidOperationException("Simulated read failure");
        f.Reader.Failure = failure;
        Assert.Same(failure, await Record.ExceptionAsync(() => f.Handler.HandleAsync(new())));
        Assert.Equal(1, f.Reader.CallCount);
    }

    private sealed class Fixture
    {
        public FakeCurrentUser User { get; } = new() { UserId = Guid.NewGuid() };
        public Reader Reader { get; } = new();
        public GetStockReservationsQueryHandler Handler { get; }
        public Fixture()
        {
            User.Permissions.Add(StockFlowPolicies.InventoryRead);
            Handler = new(User, Reader);
        }
    }

    private sealed class Reader : IStockReservationReadService
    {
        public GetStockReservationsResult Result { get; } = new(Array.Empty<StockReservationDto>(), 2, 10, 17);
        public Exception? Failure { get; set; }
        public int CallCount { get; private set; }
        public int PageNumber { get; private set; }
        public int PageSize { get; private set; }
        public StockReservationStatus? Status { get; private set; }
        public string? Search { get; private set; }
        public Guid? InventoryId { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task<GetStockReservationsResult> GetPageAsync(
            int pageNumber, int pageSize, StockReservationStatus? status,
            string? search, Guid? inventoryId, CancellationToken cancellationToken = default)
        {
            CallCount++;
            PageNumber = pageNumber;
            PageSize = pageSize;
            Status = status;
            Search = search;
            InventoryId = inventoryId;
            Token = cancellationToken;
            return Failure is null ? Task.FromResult(Result) : Task.FromException<GetStockReservationsResult>(Failure);
        }
        public Task<StockReservationDto?> GetByIdAsync(Guid reservationId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Detail lookup is not used by these list tests.");
        }
    }
}
