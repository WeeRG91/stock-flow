using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Application.Tests.Fakes;
using StockFlow.Application.Transfers.UpdateDraftTransfer;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.Application.Tests;

public sealed class UpdateDraftTransferCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_PreservesExistingSnapshotsAndLoadsNewProducts()
    {
        var fixture = new Fixture();
        var original = Assert.Single(fixture.Transfer.Lines);
        var product = new Product("Desk", "DESK");
        fixture.Products.Add(product);

        using var cancellation = new CancellationTokenSource();
        var command = fixture.Command(
            new(original.ProductId, 8),
            new(product.Id, 2));

        await fixture.Handler.HandleAsync(command, cancellation.Token);

        Assert.Same(original, fixture.Transfer.Lines[0]);
        Assert.Equal("CHAIR", original.ProductSku);
        Assert.Equal("Original chair", original.ProductName);
        Assert.Equal(8, original.Quantity);

        var added = fixture.Transfer.Lines[1];
        Assert.Equal(product.Id, added.ProductId);
        Assert.Equal(product.Sku, added.ProductSku);
        Assert.Equal(product.Name, added.ProductName);
        Assert.Equal(2, added.Quantity);

        // The retained product is deliberately absent from the product fake.
        Assert.Equal(1, fixture.Products.GetByIdCallCount);
        Assert.Equal(1, fixture.Transfers.SaveCallCount);
        Assert.Same(fixture.Transfer, fixture.Transfers.LastSavedTransfer);
        Assert.Equal(cancellation.Token, fixture.Transfers.LookupToken);
        Assert.Equal(cancellation.Token, fixture.Transfers.SaveToken);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyLines_ClearsDraft()
    {
        var fixture = new Fixture();

        await fixture.Handler.HandleAsync(fixture.Command());

        Assert.Empty(fixture.Transfer.Lines);
        Assert.Equal(0, fixture.Products.GetByIdCallCount);
        Assert.Equal(1, fixture.Transfers.SaveCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_WithUnavailableNewProduct_PreservesOriginalLines(
        bool inactive)
    {
        var fixture = new Fixture();
        var original = Assert.Single(fixture.Transfer.Lines);
        var product = new Product("Desk", "DESK");

        if (inactive)
        {
            product.Deactivate();
            fixture.Products.Add(product);
        }

        var command = fixture.Command(
            new(original.ProductId, 8),
            new(product.Id, 2));

        if (inactive)
        {
            await Assert.ThrowsAsync<BusinessRuleException>(
                () => fixture.Handler.HandleAsync(command));
        }
        else
        {
            await Assert.ThrowsAsync<ResourceNotFoundException>(
                () => fixture.Handler.HandleAsync(command));
        }

        Assert.Same(original, Assert.Single(fixture.Transfer.Lines));
        Assert.Equal(4, original.Quantity);
        Assert.Equal(0, fixture.Transfers.SaveCallCount);
    }

    [Fact]
    public async Task HandleAsync_WithStaleVersion_RejectsBeforeProductLookup()
    {
        var fixture = new Fixture();
        var command = fixture.Command() with { ExpectedVersion = Guid.NewGuid() };

        await Assert.ThrowsAsync<TransferConflictException>(
            () => fixture.Handler.HandleAsync(command));

        Assert.Single(fixture.Transfer.Lines);
        Assert.Equal(0, fixture.Products.GetByIdCallCount);
        Assert.Equal(0, fixture.Transfers.SaveCallCount);
    }

    [Fact]
    public async Task HandleAsync_AfterDispatch_RejectsEditing()
    {
        var fixture = new Fixture();
        fixture.Transfer.Dispatch(Fixture.CreatedAt.AddMinutes(1), Guid.NewGuid());

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => fixture.Handler.HandleAsync(fixture.Command()));

        Assert.Equal(TransferStatus.Dispatched, fixture.Transfer.Status);
        Assert.Equal(4, Assert.Single(fixture.Transfer.Lines).InTransitQuantity);
        Assert.Equal(0, fixture.Transfers.SaveCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_WithoutAccess_RejectsBeforeLookup(bool authenticated)
    {
        var fixture = new Fixture();
        fixture.User.Permissions.Clear();

        if (!authenticated)
        {
            fixture.User.UserId = null;
        }

        if (authenticated)
        {
            await Assert.ThrowsAsync<PermissionDeniedException>(
                () => fixture.Handler.HandleAsync(fixture.Command()));
        }
        else
        {
            await Assert.ThrowsAsync<AuthenticationRequiredException>(
                () => fixture.Handler.HandleAsync(fixture.Command()));
        }

        Assert.Equal(0, fixture.Transfers.LookupCallCount);
        Assert.Equal(0, fixture.Transfers.SaveCallCount);
    }

    private sealed class Fixture
    {
        public static readonly DateTimeOffset CreatedAt =
            new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

        private readonly Guid _version = Guid.NewGuid();

        public FakeCurrentUser User { get; } = new() { UserId = Guid.NewGuid() };
        public FakeTransferRepository Transfers { get; } = new();
        public FakeProductRepository Products { get; } = new();
        public Transfer Transfer { get; }
        public UpdateDraftTransferCommandHandler Handler { get; }

        public Fixture()
        {
            User.Permissions.Add(StockFlowPolicies.TransfersManage);
            Transfer = new Transfer(
                Guid.NewGuid(), "BRU", "Brussels",
                Guid.NewGuid(), "LIE", "Liège",
                CreatedAt, User.UserId!.Value);

            Transfer.AddLine(Guid.NewGuid(), "CHAIR", "Original chair", 4);
            Transfers.Loaded = (Transfer, _version);
            Handler = new UpdateDraftTransferCommandHandler(Transfers, User, Products);
        }

        public UpdateDraftTransferCommand Command(
            params UpdateDraftTransferLineCommand[] lines)
        {
            return new UpdateDraftTransferCommand(Transfer.Id, _version, lines);
        }
    }
}