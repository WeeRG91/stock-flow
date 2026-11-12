using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Application.PurchaseOrders.UpdateDraftPurchaseOrder;
using StockFlow.Application.Tests.Fakes;
using StockFlow.Domain.Entities;

namespace StockFlow.Application.Tests;

public class UpdateDraftPurchaseOrderCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithCurrentVersion_SavesUpdatedDraftOnce()
    {
        var setup = new TestSetup();
        using var cancellation = new CancellationTokenSource();

        await setup.Handler.HandleAsync(
            setup.Command(setup.EditedLine, setup.NewLine),
            cancellation.Token);

        Assert.Equal(1, setup.Orders.SaveCallCount);
        Assert.Equal(cancellation.Token, setup.Orders.SaveToken);
        Assert.Same(setup.Order, setup.Orders.LastSavedOrder);
        Assert.Equal(1, setup.Products.GetByIdCallCount);

        var retained = setup.Order.Lines.Single(
            line => line.ProductId == setup.OriginalLine.ProductId);

        Assert.Equal(setup.OriginalLine.Id, retained.Id);
        Assert.Equal(setup.OriginalLine.ProductName, retained.ProductName);
        Assert.Equal(2, retained.OrderedQuantity);
        Assert.Equal(23m, setup.Order.TotalAmount);
    }

    [Fact]
    public async Task HandleAsync_WithStaleVersion_LeavesDraftUntouched()
    {
        var setup = new TestSetup();
        var command = setup.Command(setup.NewLine) with
        {
            ExpectedVersion = Guid.NewGuid()
        };

        var conflict = await Assert.ThrowsAsync<PurchaseOrderConflictException>(
            () => setup.Handler.HandleAsync(command));

        Assert.Equal(setup.Order.Id, conflict.PurchaseOrderId);
        Assert.Equal(0, setup.Products.GetByIdCallCount);
        setup.AssertOriginalDraftWasNotSaved();
    }

    [Fact]
    public async Task HandleAsync_WithSubmittedOrder_RejectsEdit()
    {
        var setup = new TestSetup();
        setup.Order.Submit(setup.Order.CreatedAt.AddMinutes(1));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => setup.Handler.HandleAsync(setup.Command(setup.NewLine)));

        Assert.Equal(0, setup.Products.GetByIdCallCount);
        setup.AssertOriginalDraftWasNotSaved();
    }

    [Fact]
    public async Task HandleAsync_WithInvalidFinalLine_LeavesOriginalLinesInPlace()
    {
        var setup = new TestSetup();
        var invalid = setup.NewLine with { OrderedQuantity = 0 };

        var error = await Assert.ThrowsAsync<RequestValidationException>(
            () => setup.Handler.HandleAsync(setup.Command(setup.EditedLine, invalid)));

        Assert.Equal("Lines", error.FieldName);
        setup.AssertOriginalDraftWasNotSaved();
    }

    [Theory]
    [InlineData(false, typeof(ResourceNotFoundException))]
    [InlineData(true, typeof(BusinessRuleException))]
    public async Task HandleAsync_WithUnavailableNewProduct_LeavesDraftUntouched(
        bool inactive,
        Type expectedException)
    {
        var setup = new TestSetup();
        var line = setup.NewLine;

        if (inactive)
        {
            setup.NewProduct.Deactivate();
        }
        else
        {
            line = line with { ProductId = Guid.NewGuid() };
        }

        await Assert.ThrowsAsync(
            expectedException,
            () => setup.Handler.HandleAsync(setup.Command(setup.EditedLine, line)));

        setup.AssertOriginalDraftWasNotSaved();
    }

    [Fact]
    public async Task HandleAsync_WithoutManagePermission_SkipsLookup()
    {
        var setup = new TestSetup();
        setup.Actor.Permissions.Clear();

        await Assert.ThrowsAsync<PermissionDeniedException>(
            () => setup.Handler.HandleAsync(setup.Command(setup.NewLine)));

        Assert.Equal(0, setup.Orders.LookupCallCount);
        setup.AssertOriginalDraftWasNotSaved();
    }

    private sealed class TestSetup
    {
        public FakeCurrentUser Actor { get; } = new() { UserId = Guid.NewGuid() };
        public FakePurchaseOrderRepository Orders { get; } = new();
        public FakeProductRepository Products { get; } = new();
        public Guid Version { get; } = Guid.NewGuid();
        public Product NewProduct { get; } = new("Dock", "DOCK-001");
        public PurchaseOrder Order { get; }
        public PurchaseOrderLine OriginalLine { get; }
        public UpdateDraftPurchaseOrderCommandHandler Handler { get; }

        public TestSetup()
        {
            Actor.Permissions.Add(StockFlowPolicies.PurchaseOrdersManage);

            Order = new PurchaseOrder(
                "PO-EDIT-001",
                Guid.NewGuid(), "SUP-001", "Supplier",
                Guid.NewGuid(), "BRU-01", "Warehouse",
                new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero),
                Actor.UserId!.Value);

            Order.AddLine(Guid.NewGuid(), "LAPTOP", "Original laptop", 1, 10m);
            OriginalLine = Assert.Single(Order.Lines);

            Orders.Loaded = (Order, Version);
            Products.Add(NewProduct);
            Handler = new UpdateDraftPurchaseOrderCommandHandler(Actor, Orders, Products);
        }

        public UpdateDraftPurchaseOrderLineCommand EditedLine =>
            new(OriginalLine.ProductId, 2, 9m);

        public UpdateDraftPurchaseOrderLineCommand NewLine =>
            new(NewProduct.Id, 1, 5m);

        public UpdateDraftPurchaseOrderCommand Command(
            params UpdateDraftPurchaseOrderLineCommand[] lines) =>
            new(Order.Id, Version, lines);

        public void AssertOriginalDraftWasNotSaved()
        {
            Assert.Equal(0, Orders.SaveCallCount);
            Assert.Same(OriginalLine, Assert.Single(Order.Lines));
            Assert.Equal(10m, Order.TotalAmount);
        }
    }
}