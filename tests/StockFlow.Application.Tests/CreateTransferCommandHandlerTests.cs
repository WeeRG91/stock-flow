using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Application.Tests.Fakes;
using StockFlow.Application.Transfers.CreateTransfer;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.Application.Tests;

public sealed class CreateTransferCommandHandlerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_WithValidRequest_SavesDraftAndSnapshots(
        bool emptyDraft)
    {
        var fixture = new Fixture();
        var command = fixture.Command();
        if (emptyDraft)
        {
            command = command with { Lines = [] };
        }

        using var cancellation = new CancellationTokenSource();

        var result = await fixture.Handler.HandleAsync(command, cancellation.Token);

        var transfer = Assert.IsType<Transfer>(fixture.Transfers.LastSavedTransfer);
        Assert.Equal(1, fixture.Transfers.SaveCallCount);
        Assert.Equal(cancellation.Token, fixture.Transfers.SaveToken);
        Assert.Equal(transfer.Id, result.TransferId);
        Assert.Equal(TransferStatus.Draft, result.Status);
        Assert.Equal(fixture.Clock.UtcNow, result.CreatedAt);
        Assert.Equal(fixture.User.UserId, result.CreatedByUserId);

        Assert.Equal(fixture.Source.Id, transfer.SourceWarehouseId);
        Assert.Equal(fixture.Source.Code, transfer.SourceWarehouseCode);
        Assert.Equal(fixture.Source.Name, transfer.SourceWarehouseName);
        Assert.Equal(fixture.Destination.Id, transfer.DestinationWarehouseId);
        Assert.Equal(fixture.Destination.Code, transfer.DestinationWarehouseCode);
        Assert.Equal(fixture.Destination.Name, transfer.DestinationWarehouseName);

        if (emptyDraft)
        {
            Assert.Empty(transfer.Lines);
            Assert.Equal(0, fixture.Products.GetByIdCallCount);
        }
        else
        {
            var line = Assert.Single(transfer.Lines);
            Assert.Equal(fixture.Product.Id, line.ProductId);
            Assert.Equal(fixture.Product.Sku, line.ProductSku);
            Assert.Equal(fixture.Product.Name, line.ProductName);
            Assert.Equal(4, line.Quantity);
            Assert.Equal(0, line.DispatchedQuantity);
            Assert.Equal(0, line.InTransitQuantity);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_WithoutAccess_RejectsBeforeLookups(
        bool authenticated)
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

        Assert.Equal(0, fixture.Warehouses.GetByIdCallCount);
        Assert.Equal(0, fixture.Products.GetByIdCallCount);
        Assert.Equal(0, fixture.Transfers.SaveCallCount);
    }

    [Theory]
    [InlineData("same-warehouse", "DestinationWarehouseId")]
    [InlineData("duplicate-product", "Lines[1].ProductId")]
    [InlineData("zero-quantity", "Lines[0].Quantity")]
    public async Task HandleAsync_WithInvalidRequest_RejectsBeforeLookups(
        string scenario,
        string expectedField)
    {
        var fixture = new Fixture();
        var command = fixture.Command();

        command = scenario switch
        {
            "same-warehouse" => command with
            {
                DestinationWarehouseId = command.SourceWarehouseId
            },
            "duplicate-product" => command with
            {
                Lines = [command.Lines[0], command.Lines[0]]
            },
            _ => command with
            {
                Lines = [new CreateTransferLineCommand(fixture.Product.Id, 0)]
            }
        };

        var error = await Assert.ThrowsAsync<RequestValidationException>(
            () => fixture.Handler.HandleAsync(command));

        Assert.Equal(expectedField, error.FieldName);
        Assert.Equal(0, fixture.Warehouses.GetByIdCallCount);
        Assert.Equal(0, fixture.Products.GetByIdCallCount);
        Assert.Equal(0, fixture.Transfers.SaveCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenSaveFails_PropagatesFailureWithoutRetry()
    {
        var fixture = new Fixture();
        var failure = new InvalidOperationException("Simulated persistence failure.");
        fixture.Transfers.SaveFailure = failure;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Handler.HandleAsync(fixture.Command()));

        Assert.Same(failure, error);
        Assert.Equal(1, fixture.Transfers.SaveCallCount);
        Assert.Null(fixture.Transfers.LastSavedTransfer);
    }

    [Theory]
    [InlineData("source", false)]
    [InlineData("destination", false)]
    [InlineData("product", false)]
    [InlineData("source", true)]
    [InlineData("destination", true)]
    [InlineData("product", true)]
    public async Task HandleAsync_WithUnavailableResource_DoesNotSave(
    string resource,
    bool inactive)
    {
        var fixture = new Fixture();
        var command = fixture.Command();

        if (inactive)
        {
            switch (resource)
            {
                case "source":
                    fixture.Source.Deactivate();
                    break;
                case "destination":
                    fixture.Destination.Deactivate();
                    break;
                case "product":
                    fixture.Product.Deactivate();
                    break;
            }
        }
        else
        {
            command = resource switch
            {
                "source" => command with { SourceWarehouseId = Guid.NewGuid() },
                "destination" => command with { DestinationWarehouseId = Guid.NewGuid() },
                _ => command with
                {
                    Lines = [new CreateTransferLineCommand(Guid.NewGuid(), 4)]
                }
            };
        }

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

        Assert.Equal(0, fixture.Transfers.SaveCallCount);
        Assert.Null(fixture.Transfers.LastSavedTransfer);
    }

    private sealed class Fixture
    {
        public FakeCurrentUser User { get; } = new() { UserId = Guid.NewGuid() };
        public FakeClock Clock { get; } = new();
        public FakeWarehouseRepository Warehouses { get; } = new();
        public FakeProductRepository Products { get; } = new();
        public FakeTransferRepository Transfers { get; } = new();

        public Warehouse Source { get; } = new("BRU", "Brussels", "Belgium");
        public Warehouse Destination { get; } = new("LIE", "Liège", "Belgium");
        public Product Product { get; } = new("Office chair", "CHAIR");
        public CreateTransferCommandHandler Handler { get; }

        public Fixture()
        {
            User.Permissions.Add(StockFlowPolicies.TransfersManage);
            Warehouses.Add(Source);
            Warehouses.Add(Destination);
            Products.Add(Product);

            Handler = new CreateTransferCommandHandler(
                User, Warehouses, Products, Transfers, Clock);
        }

        public CreateTransferCommand Command()
        {
            return new CreateTransferCommand(
                Source.Id, Destination.Id,
                [new CreateTransferLineCommand(Product.Id, 4)]);
        }
    }
}