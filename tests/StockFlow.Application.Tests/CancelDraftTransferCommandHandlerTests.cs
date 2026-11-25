using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Application.Tests.Fakes;
using StockFlow.Application.Transfers.CancelDraftTransfer;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.Application.Tests;

public sealed class CancelDraftTransferCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_CancelsWithCurrentActorAndClockWithoutChangingLines()
    {
        var fixture = new Fixture();
        var line = Assert.Single(fixture.Transfer.Lines);
        using var cancellation = new CancellationTokenSource();

        await fixture.Handler.HandleAsync(fixture.Command, cancellation.Token);

        Assert.Equal(TransferStatus.Cancelled, fixture.Transfer.Status);
        Assert.Equal(fixture.Clock.UtcNow, fixture.Transfer.CancelledAt);
        Assert.Equal(fixture.User.UserId, fixture.Transfer.CancelledByUserId);
        Assert.Same(line, Assert.Single(fixture.Transfer.Lines));
        Assert.Equal(4, line.Quantity);
        Assert.Equal(0, line.DispatchedQuantity);
        Assert.Empty(fixture.Transfer.PendingReceipts);
        Assert.Equal(1, fixture.Transfers.CancellationSaveCallCount);
        Assert.Equal(0, fixture.Transfers.SaveCallCount);
        Assert.Same(fixture.Transfer, fixture.Transfers.LastSavedTransfer);
        Assert.Equal(cancellation.Token, fixture.Transfers.LookupToken);
        Assert.Equal(cancellation.Token, fixture.Transfers.SaveToken);
        Assert.Equal(fixture.Transfer.Id,
            Assert.Single(fixture.Notifier.Notifications).TransferId);
        Assert.Equal(cancellation.Token, fixture.Notifier.LastToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_WithoutAccess_RejectsBeforeLookup(bool authenticated)
    {
        var fixture = new Fixture();
        fixture.User.Permissions.Clear();
        if (!authenticated) fixture.User.UserId = null;

        if (authenticated)
            await Assert.ThrowsAsync<PermissionDeniedException>(
                () => fixture.Handler.HandleAsync(fixture.Command));
        else
            await Assert.ThrowsAsync<AuthenticationRequiredException>(
                () => fixture.Handler.HandleAsync(fixture.Command));

        Assert.Equal(0, fixture.Transfers.LookupCallCount);
        AssertUnchangedDraft(fixture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_WithEmptyIdentifier_RejectsBeforeLookup(bool emptyVersion)
    {
        var fixture = new Fixture();
        var command = emptyVersion
            ? fixture.Command with { ExpectedVersion = Guid.Empty }
            : fixture.Command with { TransferId = Guid.Empty };

        await Assert.ThrowsAsync<RequestValidationException>(
            () => fixture.Handler.HandleAsync(command));

        Assert.Equal(0, fixture.Transfers.LookupCallCount);
        AssertUnchangedDraft(fixture);
    }

    [Fact]
    public async Task HandleAsync_WithMissingTransfer_ReturnsNotFound()
    {
        var fixture = new Fixture();
        fixture.Transfers.Loaded = null;

        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => fixture.Handler.HandleAsync(fixture.Command));

        AssertUnchangedDraft(fixture);
    }

    [Fact]
    public async Task HandleAsync_WithStaleVersion_DoesNotCancel()
    {
        var fixture = new Fixture();
        var command = fixture.Command with { ExpectedVersion = Guid.NewGuid() };

        await Assert.ThrowsAsync<TransferConflictException>(
            () => fixture.Handler.HandleAsync(command));

        AssertUnchangedDraft(fixture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_WithNonDraftTransfer_DoesNotSave(bool alreadyCancelled)
    {
        var fixture = new Fixture();
        if (alreadyCancelled)
            fixture.Transfer.Cancel(fixture.Clock.UtcNow, fixture.User.UserId!.Value);
        else
            fixture.Transfer.Dispatch(fixture.Clock.UtcNow, fixture.User.UserId!.Value);
        var originalStatus = fixture.Transfer.Status;

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => fixture.Handler.HandleAsync(fixture.Command));

        Assert.Equal(originalStatus, fixture.Transfer.Status);
        Assert.Equal(0, fixture.Transfers.CancellationSaveCallCount);
        Assert.Equal(0, fixture.Transfers.SaveCallCount);
        Assert.Empty(fixture.Notifier.Notifications);
    }

    [Fact]
    public async Task HandleAsync_WithCancelledToken_DoesNotLookupOrSave()
    {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Handler.HandleAsync(fixture.Command, cancellation.Token));

        Assert.Equal(0, fixture.Transfers.LookupCallCount);
        AssertUnchangedDraft(fixture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_WhenSaveFails_PropagatesFailureWithoutRetry(bool conflict)
    {
        var fixture = new Fixture();
        Exception failure = conflict
            ? new TransferConflictException(fixture.Transfer.Id, "Stale transfer.")
            : new InvalidOperationException("Simulated persistence failure.");
        fixture.Transfers.SaveFailure = failure;

        var error = await Record.ExceptionAsync(
            () => fixture.Handler.HandleAsync(fixture.Command));

        Assert.Same(failure, error);
        Assert.Equal(1, fixture.Transfers.CancellationSaveCallCount);
        Assert.Equal(0, fixture.Transfers.SaveCallCount);
        Assert.Null(fixture.Transfers.LastSavedTransfer);
        Assert.Empty(fixture.Notifier.Notifications);
        // Domain mutation remains in memory; this does not imply a committed save.
        Assert.Equal(TransferStatus.Cancelled, fixture.Transfer.Status);
    }

    private static void AssertUnchangedDraft(Fixture fixture)
    {
        Assert.Equal(TransferStatus.Draft, fixture.Transfer.Status);
        Assert.Null(fixture.Transfer.CancelledAt);
        Assert.Null(fixture.Transfer.CancelledByUserId);
        Assert.Equal(4, Assert.Single(fixture.Transfer.Lines).Quantity);
        Assert.Equal(0, fixture.Transfers.CancellationSaveCallCount);
        Assert.Equal(0, fixture.Transfers.SaveCallCount);
        Assert.Empty(fixture.Notifier.Notifications);
    }

    private sealed class Fixture
    {
        public FakeCurrentUser User { get; } = new() { UserId = Guid.NewGuid() };
        public FakeTransferRepository Transfers { get; } = new();
        public FakeTransferChangeNotifier Notifier { get; } = new();
        public FakeClock Clock { get; } = new()
        {
            UtcNow = new DateTimeOffset(2026, 11, 20, 11, 0, 0, TimeSpan.Zero)
        };
        public Transfer Transfer { get; }
        public CancelDraftTransferCommand Command { get; }
        public CancelDraftTransferCommandHandler Handler { get; }

        public Fixture()
        {
            User.Permissions.Add(StockFlowPolicies.TransfersManage);
            Transfer = new Transfer(
                Guid.NewGuid(), "BRU", "Brussels",
                Guid.NewGuid(), "LIE", "Liège", Clock.UtcNow.AddHours(-1),
                User.UserId!.Value);
            Transfer.AddLine(Guid.NewGuid(), "CHAIR", "Office chair", 4);
            var version = Guid.NewGuid();
            Transfers.Loaded = (Transfer, version);
            Command = new CancelDraftTransferCommand(Transfer.Id, version);
            Notifier.OnNotify = notification =>
            {
                Assert.Equal(1, Transfers.CancellationSaveCallCount);
                Assert.Same(Transfer, Transfers.LastSavedTransfer);
                Assert.Equal(TransferStatus.Cancelled, Transfer.Status);
                Assert.Equal(Transfer.Id, notification.TransferId);
            };
            Handler = new CancelDraftTransferCommandHandler(Transfers, User, Clock, Notifier);
        }
    }
}
