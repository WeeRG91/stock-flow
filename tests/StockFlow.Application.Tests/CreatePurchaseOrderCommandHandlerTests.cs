using StockFlow.Application.Abstractions.Purchasing;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Application.PurchaseOrders.CreatePurchaseOrder;
using StockFlow.Application.Tests.Fakes;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.Application.Tests;

public class CreatePurchaseOrderCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithValidLines_SavesCompleteDraftOnce()
    {
        var setup = new TestSetup();
        var secondProduct = new Product("Docking Station", "DOCK-001");
        setup.Products.Add(secondProduct);

        using var cancellation = new CancellationTokenSource();

        var result = await setup.Handler.HandleAsync(
            setup.Command(
                new CreatePurchaseOrderLineCommand(setup.Product.Id, 2, 19.95m),
                new CreatePurchaseOrderLineCommand(secondProduct.Id, 1, 5.10m)),
            cancellation.Token);

        var saved = Assert.IsType<PurchaseOrder>(setup.Orders.LastSavedOrder);

        Assert.Equal(1, setup.Orders.SaveCallCount);
        Assert.Equal(cancellation.Token, setup.Orders.SaveToken);
        Assert.Equal(setup.Supplier.Id, saved.SupplierId);
        Assert.Equal(setup.Supplier.Code, saved.SupplierCode);
        Assert.Equal(setup.Supplier.Name, saved.SupplierName);
        Assert.Equal(setup.Warehouse.Id, saved.WarehouseId);
        Assert.Equal(setup.Warehouse.Code, saved.WarehouseCode);
        Assert.Equal(setup.Warehouse.Name, saved.WarehouseName);

        Assert.Collection(
            saved.Lines,
            first =>
            {
                Assert.Equal(setup.Product.Id, first.ProductId);
                Assert.Equal(setup.Product.Sku, first.ProductSku);
                Assert.Equal(setup.Product.Name, first.ProductName);
                Assert.Equal(2, first.OrderedQuantity);
                Assert.Equal(19.95m, first.UnitPrice);
            },
            second =>
            {
                Assert.Equal(secondProduct.Id, second.ProductId);
                Assert.Equal(5.10m, second.LineTotal);
            });

        Assert.Equal(45.00m, saved.TotalAmount);
        Assert.Equal(setup.Clock.UtcNow, saved.CreatedAt);
        Assert.Equal(setup.Actor.UserId, saved.CreatedByUserId);

        Assert.Equal(
            new CreatePurchaseOrderResult(
                saved.Id,
                "PO-TEST-001",
                PurchaseOrderStatus.Draft,
                "EUR",
                45.00m,
                setup.Clock.UtcNow,
                saved.CreatedByUserId),
            result);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyLines_SavesEmptyDraft()
    {
        var setup = new TestSetup();

        var result = await setup.Handler.HandleAsync(setup.Command());

        var saved = Assert.IsType<PurchaseOrder>(setup.Orders.LastSavedOrder);

        Assert.Equal(1, setup.Orders.SaveCallCount);
        Assert.Equal(0, setup.Products.GetByIdCallCount);
        Assert.Empty(saved.Lines);
        Assert.Equal(PurchaseOrderStatus.Draft, result.Status);
        Assert.Equal(0m, result.TotalAmount);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidSecondLine_DoesNotSave()
    {
        var setup = new TestSetup();
        var secondProduct = new Product("Docking Station", "DOCK-001");
        setup.Products.Add(secondProduct);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => setup.Handler.HandleAsync(setup.Command(
                new CreatePurchaseOrderLineCommand(setup.Product.Id, 1, 10m),
                new CreatePurchaseOrderLineCommand(secondProduct.Id, 1, 1.001m))));

        Assert.Equal("Lines[1].UnitPrice", exception.FieldName);
        Assert.Equal(0, setup.Orders.SaveCallCount);
        Assert.Null(setup.Orders.LastSavedOrder);
    }

    [Fact]
    public async Task HandleAsync_WithDuplicateProduct_ReportsSecondLine()
    {
        var setup = new TestSetup();
        var line = new CreatePurchaseOrderLineCommand(setup.Product.Id, 1, 10m);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => setup.Handler.HandleAsync(setup.Command(line, line)));

        Assert.Equal("Lines[1].ProductId", exception.FieldName);
        Assert.Equal(1, setup.Products.GetByIdCallCount);
        Assert.Equal(0, setup.Orders.SaveCallCount);
    }

    [Theory]
    [InlineData(false, typeof(AuthenticationRequiredException))]
    [InlineData(true, typeof(PermissionDeniedException))]
    public async Task HandleAsync_WithoutRequiredAccess_SkipsRepositories(
        bool authenticated,
        Type exceptionType)
    {
        var setup = new TestSetup();

        if (authenticated)
        {
            setup.Actor.Permissions.Clear();
            setup.Actor.Permissions.Add(StockFlowPolicies.PurchaseOrdersRead);
        }
        else
        {
            setup.Actor.UserId = null;
        }

        await Assert.ThrowsAsync(
            exceptionType,
            () => setup.Handler.HandleAsync(setup.Command()));

        Assert.Equal(0, setup.Suppliers.GetByIdCallCount);
        Assert.Equal(0, setup.Warehouses.GetByIdCallCount);
        Assert.Equal(0, setup.Products.GetByIdCallCount);
        Assert.Equal(0, setup.Orders.SaveCallCount);
    }

    private sealed class TestSetup
    {
        public FakeCurrentUser Actor { get; } = new() { UserId = Guid.NewGuid() };
        public FakeSupplierRepository Suppliers { get; } = new();
        public FakeWarehouseRepository Warehouses { get; } = new();
        public FakeProductRepository Products { get; } = new();
        public FakePurchaseOrderRepository Orders { get; } = new();
        public FakeClock Clock { get; } = new();

        public Supplier Supplier { get; } =
            new("SUP-001", "Nova Electronics", "sales@example.com");

        public Warehouse Warehouse { get; } =
            new("BRU-01", "Brussels Warehouse", "Brussels");

        public Product Product { get; } =
            new("Business Laptop", "LAPTOP-001");

        public CreatePurchaseOrderCommandHandler Handler { get; }

        public TestSetup()
        {
            Actor.Permissions.Add(StockFlowPolicies.PurchaseOrdersManage);
            Suppliers.Add(Supplier);
            Warehouses.Add(Warehouse);
            Products.Add(Product);

            Handler = new CreatePurchaseOrderCommandHandler(
                Actor, Suppliers, Warehouses, Products,
                Orders, new FixedOrderNumberGenerator(), Clock);
        }

        public CreatePurchaseOrderCommand Command(
            params CreatePurchaseOrderLineCommand[] lines) =>
            new(Supplier.Id, Warehouse.Id, lines);
    }

    private sealed class FixedOrderNumberGenerator : IPurchaseOrderNumberGenerator
    {
        public string Generate() => "PO-TEST-001";
    }

    [Theory]
    [InlineData("Supplier")]
    [InlineData("Warehouse")]
    [InlineData("Product")]
    public async Task HandleAsync_WithMissingCatalogResource_DoesNotSave(
    string resource)
    {
        var setup = new TestSetup();
        var missingId = Guid.NewGuid();
        var line = new CreatePurchaseOrderLineCommand(setup.Product.Id, 1, 10m);

        var command = resource switch
        {
            "Supplier" => setup.Command(line) with { SupplierId = missingId },
            "Warehouse" => setup.Command(line) with { WarehouseId = missingId },
            "Product" => setup.Command(line with { ProductId = missingId }),
            _ => throw new ArgumentOutOfRangeException(nameof(resource))
        };

        var exception = await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => setup.Handler.HandleAsync(command));

        Assert.Equal(resource, exception.ResourceName);
        Assert.Equal(missingId, exception.ResourceId);
        Assert.Equal(0, setup.Orders.SaveCallCount);
    }

    [Theory]
    [InlineData("Supplier")]
    [InlineData("Warehouse")]
    [InlineData("Product")]
    public async Task HandleAsync_WithInactiveCatalogResource_DoesNotSave(
        string resource)
    {
        var setup = new TestSetup();

        switch (resource)
        {
            case "Supplier":
                setup.Supplier.Deactivate();
                break;
            case "Warehouse":
                setup.Warehouse.Deactivate();
                break;
            case "Product":
                setup.Product.Deactivate();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(resource));
        }

        var command = setup.Command(
            new CreatePurchaseOrderLineCommand(setup.Product.Id, 1, 10m));

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => setup.Handler.HandleAsync(command));

        Assert.Equal($"{resource} is inactive.", exception.Message);
        Assert.Equal(0, setup.Orders.SaveCallCount);
    }

    [Theory]
    [InlineData("SupplierId")]
    [InlineData("WarehouseId")]
    [InlineData("Lines")]
    public async Task HandleAsync_WithInvalidRequest_SkipsCatalogLookups(
    string field)
    {
        var setup = new TestSetup();
        var valid = setup.Command();

        var command = field switch
        {
            "SupplierId" => valid with { SupplierId = Guid.Empty },
            "WarehouseId" => valid with { WarehouseId = Guid.Empty },
            "Lines" => valid with { Lines = null! },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => setup.Handler.HandleAsync(command));

        Assert.Equal(field, exception.FieldName);
        Assert.Equal(0, setup.Suppliers.GetByIdCallCount);
        Assert.Equal(0, setup.Warehouses.GetByIdCallCount);
        Assert.Equal(0, setup.Products.GetByIdCallCount);
        Assert.Equal(0, setup.Orders.SaveCallCount);
    }

    [Theory]
    [InlineData("null", "Lines[0]")]
    [InlineData("product", "Lines[0].ProductId")]
    [InlineData("quantity", "Lines[0].OrderedQuantity")]
    [InlineData("overflow", "Lines[0]")]
    public async Task HandleAsync_WithMalformedLine_ReportsIndexedField(
        string input,
        string expectedField)
    {
        var setup = new TestSetup();
        var valid = new CreatePurchaseOrderLineCommand(setup.Product.Id, 1, 10m);

        CreatePurchaseOrderLineCommand line = input switch
        {
            "null" => null!,
            "product" => valid with { ProductId = Guid.Empty },
            "quantity" => valid with { OrderedQuantity = 0 },
            "overflow" => valid with { OrderedQuantity = 2, UnitPrice = decimal.MaxValue },
            _ => throw new ArgumentOutOfRangeException(nameof(input))
        };

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => setup.Handler.HandleAsync(setup.Command(line)));

        Assert.Equal(expectedField, exception.FieldName);
        Assert.Equal(0, setup.Orders.SaveCallCount);
    }

    [Fact]
    public async Task HandleAsync_WithCanceledToken_SkipsCatalogLookupsAndSave()
    {
        var setup = new TestSetup();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => setup.Handler.HandleAsync(setup.Command(), cancellation.Token));

        Assert.Equal(0, setup.Suppliers.GetByIdCallCount);
        Assert.Equal(0, setup.Warehouses.GetByIdCallCount);
        Assert.Equal(0, setup.Products.GetByIdCallCount);
        Assert.Equal(0, setup.Orders.SaveCallCount);
    }
}