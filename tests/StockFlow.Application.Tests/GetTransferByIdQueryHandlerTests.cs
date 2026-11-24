using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Application.Tests.Fakes;
using StockFlow.Application.Transfers;
using StockFlow.Application.Transfers.GetTransferById;
using StockFlow.Application.Transfers.GetTransfers;
using StockFlow.Domain.Enums;

namespace StockFlow.Application.Tests;

public sealed class GetTransferByIdQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsDetailsAndForwardsIdAndToken()
    {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();

        var result = await fixture.Handler.HandleAsync(fixture.Query, cancellation.Token);

        Assert.Same(fixture.Reader.Details, result);
        Assert.Equal(fixture.Query.TransferId, fixture.Reader.RequestedId);
        Assert.Equal(cancellation.Token, fixture.Reader.Token);
        Assert.Equal(1, fixture.Reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_EmptyId_RejectsBeforeRead()
    {
        var fixture = new Fixture();
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            fixture.Handler.HandleAsync(new GetTransferByIdQuery(Guid.Empty)));
        Assert.Equal(0, fixture.Reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_MissingTransfer_ReturnsNotFound()
    {
        var fixture = new Fixture();
        fixture.Reader.Details = null;
        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            fixture.Handler.HandleAsync(fixture.Query));
        Assert.Equal(1, fixture.Reader.CallCount);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("empty-user")]
    [InlineData("manage-only")]
    public async Task HandleAsync_WithoutReadAccess_DoesNotRead(string scenario)
    {
        var fixture = new Fixture();
        fixture.User.Permissions.Clear();
        fixture.User.Permissions.Add(StockFlowPolicies.TransfersManage);
        if (scenario == "anonymous") fixture.User.UserId = null;
        if (scenario == "empty-user") fixture.User.UserId = Guid.Empty;
        if (scenario == "manage-only")
            await Assert.ThrowsAsync<PermissionDeniedException>(() => fixture.Handler.HandleAsync(fixture.Query));
        else
            await Assert.ThrowsAsync<AuthenticationRequiredException>(() => fixture.Handler.HandleAsync(fixture.Query));
        Assert.Equal(0, fixture.Reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_CancelledToken_DoesNotRead()
    {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Handler.HandleAsync(fixture.Query, cancellation.Token));
        Assert.Equal(0, fixture.Reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_ReaderFailure_PropagatesWithoutRetry()
    {
        var fixture = new Fixture();
        var failure = new InvalidOperationException("Simulated read failure.");
        fixture.Reader.Failure = failure;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Handler.HandleAsync(fixture.Query));
        Assert.Same(failure, error);
        Assert.Equal(1, fixture.Reader.CallCount);
    }

    private sealed class Fixture
    {
        public FakeCurrentUser User { get; } = new() { UserId = Guid.NewGuid() };
        public RecordingReader Reader { get; } = new();
        public GetTransferByIdQuery Query { get; }
        public GetTransferByIdQueryHandler Handler { get; }

        public Fixture()
        {
            User.Permissions.Add(StockFlowPolicies.TransfersRead);
            var id = Guid.NewGuid();
            Reader.Details = new TransferDetailsDto(id, Guid.NewGuid(), "BRU", "Brussels",
                Guid.NewGuid(), "LIE", "Liège", TransferStatus.Draft,
                new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero), User.UserId!.Value,
                null, null, null, null, null, Guid.NewGuid(), []);
            Query = new GetTransferByIdQuery(id);
            Handler = new GetTransferByIdQueryHandler(User, Reader);
        }
    }

    private sealed class RecordingReader : ITransferReadService
    {
        public TransferDetailsDto? Details { get; set; }
        public Exception? Failure { get; set; }
        public int CallCount { get; private set; }
        public Guid RequestedId { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<TransferDetailsDto?> GetByIdAsync(Guid transferId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            RequestedId = transferId;
            Token = cancellationToken;
            return Failure is { } failure ? Task.FromException<TransferDetailsDto?>(failure)
                : Task.FromResult(Details);
        }

        public Task<GetTransfersResult> GetPageAsync(int pageNumber, int pageSize,
            TransferStatus? status, string? search, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Detail tests must not request a list.");
    }
}
