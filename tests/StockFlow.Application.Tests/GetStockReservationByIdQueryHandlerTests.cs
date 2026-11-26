using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Application.StockReservations;
using StockFlow.Application.StockReservations.GetStockReservationById;
using StockFlow.Application.StockReservations.GetStockReservations;
using StockFlow.Application.Tests.Fakes;
using StockFlow.Domain.Enums;

namespace StockFlow.Application.Tests;

public sealed class GetStockReservationByIdQueryHandlerTests
{
    [Theory]
    [InlineData(StockReservationStatus.Active)]
    [InlineData(StockReservationStatus.Released)]
    [InlineData(StockReservationStatus.Fulfilled)]
    public async Task Get_ReturnsDtoAndForwardsIdentityAndCancellation(StockReservationStatus status)
    {
        var reader = new Reader { Result = CreateDto(status) };
        var handler = CreateHandler(reader);
        using var cancellation = new CancellationTokenSource();
        var result = await handler.HandleAsync(new GetStockReservationByIdQuery(reader.Result.ReservationId), cancellation.Token);
        Assert.Same(reader.Result, result);
        Assert.Equal(reader.Result.ReservationId, reader.ReceivedId);
        Assert.Equal(cancellation.Token, reader.ReceivedToken);
        Assert.Equal(1, reader.CallCount);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("emptyUser")]
    [InlineData("permission")]
    public async Task Get_WithoutAccess_DoesNotRead(string scenario)
    {
        var reader = new Reader();
        var user = new FakeCurrentUser { UserId = scenario == "anonymous" ? null : Guid.Empty };
        user.Permissions.Add(StockFlowPolicies.InventoryRead);
        if (scenario == "permission")
        {
            user.UserId = Guid.NewGuid();
            user.Permissions.Clear();
            user.Permissions.Add(StockFlowPolicies.StockReservationsManage);
        }
        var handler = new GetStockReservationByIdQueryHandler(user, reader);
        if (scenario == "permission")
            await Assert.ThrowsAsync<PermissionDeniedException>(() => handler.HandleAsync(new(Guid.NewGuid())));
        else
            await Assert.ThrowsAsync<AuthenticationRequiredException>(() => handler.HandleAsync(new(Guid.NewGuid())));
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task Get_WithNullQuery_DoesNotRead()
    {
        var reader = new Reader();
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateHandler(reader).HandleAsync(null!));
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task Get_WithEmptyId_ReturnsValidationWithoutReading()
    {
        var reader = new Reader();
        var error = await Assert.ThrowsAsync<RequestValidationException>(() => CreateHandler(reader).HandleAsync(new(Guid.Empty)));
        Assert.Equal("ReservationId", error.FieldName);
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task Get_WhenMissing_ThrowsNotFound()
    {
        var reader = new Reader();
        var id = Guid.NewGuid();
        var error = await Assert.ThrowsAsync<ResourceNotFoundException>(() => CreateHandler(reader).HandleAsync(new(id)));
        Assert.Equal("Stock reservation", error.ResourceName);
        Assert.Equal(id, error.ResourceId);
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public async Task Get_WhenCancelled_DoesNotRead()
    {
        var reader = new Reader();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateHandler(reader).HandleAsync(new(Guid.NewGuid()), cancellation.Token));
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task Get_WhenReaderFails_PropagatesFailure()
    {
        var failure = new InvalidOperationException("Simulated query failure");
        var reader = new Reader { Failure = failure };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler(reader).HandleAsync(new(Guid.NewGuid())));
        Assert.Same(failure, error);
        Assert.Equal(1, reader.CallCount);
    }

    private static GetStockReservationByIdQueryHandler CreateHandler(Reader reader)
    {
        var user = new FakeCurrentUser { UserId = Guid.NewGuid() };
        user.Permissions.Add(StockFlowPolicies.InventoryRead);
        return new(user, reader);
    }

    private static StockReservationDto CreateDto(StockReservationStatus status)
    {
        var created = new DateTimeOffset(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);
        return new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Synthetic product", "TEST-PRODUCT",
            Guid.NewGuid(), "Synthetic warehouse", "TEST-BRU", 30, "Operation", status, created, Guid.NewGuid(),
            status == StockReservationStatus.Released ? created.AddHours(1) : null,
            status == StockReservationStatus.Released ? Guid.NewGuid() : null,
            status == StockReservationStatus.Fulfilled ? created.AddHours(1) : null,
            status == StockReservationStatus.Fulfilled ? Guid.NewGuid() : null, Guid.NewGuid());
    }

    private sealed class Reader : IStockReservationReadService
    {
        public Task<GetStockReservationsResult> GetPageAsync(
            int pageNumber, int pageSize, StockReservationStatus? status,
            string? search, Guid? inventoryId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("List queries are not used by these detail tests.");
        }

        public StockReservationDto? Result { get; set; }
        public Exception? Failure { get; set; }
        public int CallCount { get; private set; }
        public Guid ReceivedId { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }
        public Task<StockReservationDto?> GetByIdAsync(Guid reservationId, CancellationToken cancellationToken = default)
        {
            CallCount++;
            ReceivedId = reservationId;
            ReceivedToken = cancellationToken;
            return Failure is null ? Task.FromResult(Result) : Task.FromException<StockReservationDto?>(Failure);
        }
    }
}
