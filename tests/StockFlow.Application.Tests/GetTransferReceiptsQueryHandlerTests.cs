using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Application.Tests.Fakes;
using StockFlow.Application.Transfers;
using StockFlow.Application.Transfers.GetTransferReceipts;
using StockFlow.Application.Transfers.GetTransfers;
using StockFlow.Domain.Enums;

namespace StockFlow.Application.Tests;

public sealed class GetTransferReceiptsQueryHandlerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_ReturnsHistoryOrEmptyListAndForwardsToken(bool empty)
    {
        var fixture = new Fixture();
        if (empty) fixture.Reader.History = [];
        using var cancellation = new CancellationTokenSource();

        var result = await fixture.Handler.HandleAsync(fixture.Query, cancellation.Token);

        Assert.Same(fixture.Reader.History, result);
        Assert.Equal(fixture.Query.TransferId, fixture.Reader.RequestedId);
        Assert.Equal(cancellation.Token, fixture.Reader.Token);
        Assert.Equal(1, fixture.Reader.CallCount);
        if (empty) Assert.Empty(result);
        else Assert.Single(result);
    }

    [Fact]
    public async Task HandleAsync_MissingTransfer_ThrowsNotFound()
    {
        var fixture = new Fixture();
        fixture.Reader.History = null;
        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            fixture.Handler.HandleAsync(fixture.Query));
        Assert.Equal(1, fixture.Reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_EmptyId_DoesNotRead()
    {
        var fixture = new Fixture();
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            fixture.Handler.HandleAsync(new GetTransferReceiptsQuery(Guid.Empty)));
        Assert.Equal(0, fixture.Reader.CallCount);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("empty-user")]
    [InlineData("receive-only")]
    public async Task HandleAsync_WithoutReadPermission_DoesNotRead(string scenario)
    {
        var fixture = new Fixture();
        fixture.User.Permissions.Clear();
        fixture.User.Permissions.Add(StockFlowPolicies.TransfersReceive);
        if (scenario == "anonymous") fixture.User.UserId = null;
        if (scenario == "empty-user") fixture.User.UserId = Guid.Empty;

        if (scenario == "receive-only")
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
        var failure = new InvalidOperationException("Simulated history read failure.");
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
        public GetTransferReceiptsQuery Query { get; } = new(Guid.NewGuid());
        public GetTransferReceiptsQueryHandler Handler { get; }

        public Fixture()
        {
            // Read alone must be sufficient; no receiving or management permission.
            User.Permissions.Add(StockFlowPolicies.TransfersRead);
            Handler = new GetTransferReceiptsQueryHandler(User, Reader);
        }
    }

    private sealed class RecordingReader : ITransferReadService
    {
        public IReadOnlyList<TransferReceiptHistoryItemDto>? History { get; set; } =
        [
            new(Guid.NewGuid(), new DateTimeOffset(2026, 11, 25, 10, 0, 0, TimeSpan.Zero),
                Guid.NewGuid(), [new(Guid.NewGuid(), 2)])
        ];
        public int CallCount { get; private set; }
        public Guid RequestedId { get; private set; }
        public CancellationToken Token { get; private set; }
        public Exception? Failure { get; set; }

        public Task<IReadOnlyList<TransferReceiptHistoryItemDto>?> GetReceiptsAsync(
            Guid transferId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            RequestedId = transferId;
            Token = cancellationToken;
            return Failure is { } failure
                ? Task.FromException<IReadOnlyList<TransferReceiptHistoryItemDto>?>(failure)
                : Task.FromResult(History);
        }

        public Task<TransferDetailsDto?> GetByIdAsync(Guid transferId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("History queries must not load details.");

        public Task<GetTransfersResult> GetPageAsync(int pageNumber, int pageSize,
            TransferStatus? status, string? search, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("History queries must not load the transfer list.");
    }
}
