using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Application.Tests.Fakes;
using StockFlow.Application.Transfers;
using StockFlow.Application.Transfers.GetTransfers;
using StockFlow.Application.Transfers.GetTransferReceipts;
using StockFlow.Domain.Enums;

namespace StockFlow.Application.Tests;

public sealed class GetTransfersQueryHandlerTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(" BRU ", "BRU")]
    public async Task HandleAsync_ForwardsFiltersAndTokenAndReturnsReaderResult(
        string? search, string? expectedSearch)
    {
        var user = ReaderUser();
        var reader = new RecordingReader();
        var handler = new GetTransfersQueryHandler(user, reader);
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(
            new GetTransfersQuery(2, 10, TransferStatus.Dispatched, search), cancellation.Token);

        Assert.Same(reader.Result, result);
        Assert.Equal(1, reader.CallCount);
        Assert.Equal(new GetTransfersQuery(2, 10, TransferStatus.Dispatched, expectedSearch), reader.Query);
        Assert.Equal(cancellation.Token, reader.Token);
    }

    [Theory]
    [InlineData(0, 20, null)]
    [InlineData(-1, 20, null)]
    [InlineData(1, 0, null)]
    [InlineData(1, -1, null)]
    [InlineData(1, 101, null)]
    [InlineData(1, 20, 999)]
    [InlineData(int.MaxValue, 100, null)]
    public async Task HandleAsync_InvalidPaginationOrStatus_DoesNotRead(
        int page, int size, int? status)
    {
        var reader = new RecordingReader();
        var handler = new GetTransfersQueryHandler(ReaderUser(), reader);

        await Assert.ThrowsAsync<RequestValidationException>(() => handler.HandleAsync(
            new GetTransfersQuery(page, size, status is null ? null : (TransferStatus)status.Value)));

        Assert.Equal(0, reader.CallCount);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("empty-user")]
    [InlineData("manage-only")]
    public async Task HandleAsync_WithoutReadAccess_DoesNotRead(string scenario)
    {
        var user = ReaderUser();
        user.Permissions.Clear();
        user.Permissions.Add(StockFlowPolicies.TransfersManage);
        if (scenario == "anonymous") user.UserId = null;
        if (scenario == "empty-user") user.UserId = Guid.Empty;
        var reader = new RecordingReader();
        var handler = new GetTransfersQueryHandler(user, reader);

        if (scenario == "manage-only")
            await Assert.ThrowsAsync<PermissionDeniedException>(
                () => handler.HandleAsync(new GetTransfersQuery()));
        else
            await Assert.ThrowsAsync<AuthenticationRequiredException>(
                () => handler.HandleAsync(new GetTransfersQuery()));

        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_CancelledToken_DoesNotRead()
    {
        var reader = new RecordingReader();
        var handler = new GetTransfersQueryHandler(ReaderUser(), reader);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.HandleAsync(new GetTransfersQuery(), cancellation.Token));

        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task HandleAsync_ReaderFailure_PropagatesWithoutRetry()
    {
        var failure = new InvalidOperationException("Read failed.");
        var reader = new RecordingReader { Failure = failure };
        var handler = new GetTransfersQueryHandler(ReaderUser(), reader);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new GetTransfersQuery()));

        Assert.Same(failure, error);
        Assert.Equal(1, reader.CallCount);
    }

    private static FakeCurrentUser ReaderUser()
    {
        var user = new FakeCurrentUser { UserId = Guid.NewGuid() };
        user.Permissions.Add(StockFlowPolicies.TransfersRead);
        return user;
    }

    private sealed class RecordingReader : ITransferReadService
    {
        public Task<IReadOnlyList<TransferReceiptHistoryItemDto>?> GetReceiptsAsync(
            Guid transferId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "These tests must not request transfer receipt history.");
        }

        public Task<TransferDetailsDto?> GetByIdAsync(
            Guid transferId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "These list tests must not request transfer details.");
        }

        public GetTransfersResult Result { get; } = new([], 2, 10, 0);
        public int CallCount { get; private set; }
        public GetTransfersQuery? Query { get; private set; }
        public CancellationToken Token { get; private set; }
        public Exception? Failure { get; init; }

        public Task<GetTransfersResult> GetPageAsync(int pageNumber, int pageSize,
            TransferStatus? status, string? search, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            Query = new(pageNumber, pageSize, status, search);
            Token = cancellationToken;
            return Failure is { } failure ? Task.FromException<GetTransfersResult>(failure)
                : Task.FromResult(Result);
        }
    }
}
