using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Application.PurchaseOrders.CreatePurchaseOrder;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace StockFlow.API.Tests;

public class CreatePurchaseOrderEndpointTests
{
    private const string Endpoint = "/api/purchase-orders";

    [Fact]
    public async Task Create_AsPurchasingOfficer_ReturnsCreatedDraft()
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = [StockFlowRoles.PurchasingOfficer];

        var orders = new RecordingOrderRepository();
        using var host = CreateHost(factory, orders);
        using var client = await host.CreateClientWithAntiforgeryAsync();

        using var response = await client.PostAsJsonAsync(
            Endpoint, CreateRequest(factory));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<CreatePurchaseOrderResult>();

        Assert.NotNull(result);

        Assert.Equal(
            $"/api/purchase-orders/{result.PurchaseOrderId}",
            response.Headers.Location?.AbsolutePath);

        var saved = Assert.IsType<PurchaseOrder>(orders.SavedOrder);

        Assert.Equal(1, orders.SaveCallCount);
        Assert.Equal(saved.Id, result.PurchaseOrderId);
        Assert.Equal(factory.User.Id, result.CreatedByUserId);
        Assert.Equal(PurchaseOrderStatus.Draft, result.Status);
        Assert.Equal("EUR", result.Currency);
        Assert.Equal(39.90m, result.TotalAmount);
        Assert.Equal(factory.Product.Id, Assert.Single(saved.Lines).ProductId);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task Create_WithoutManageAccess_DoesNotSave(
        bool authenticated,
        HttpStatusCode expectedStatus)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.IsAuthenticated = authenticated;
        factory.User.Roles = [StockFlowRoles.Viewer];

        var orders = new RecordingOrderRepository();
        using var host = CreateHost(factory, orders);
        using var client = await host.CreateClientWithAntiforgeryAsync();

        using var response = await client.PostAsJsonAsync(
            Endpoint, CreateRequest(factory));

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(0, orders.SaveCallCount);
    }

    [Fact]
    public async Task Create_WithoutAntiforgeryToken_DoesNotSave()
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = [StockFlowRoles.PurchasingOfficer];

        var orders = new RecordingOrderRepository();
        using var host = CreateHost(factory, orders);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        using var response = await client.PostAsJsonAsync(
            Endpoint, CreateRequest(factory));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, orders.SaveCallCount);
    }

    private static object CreateRequest(StockFlowApiFactory factory)
    {
        var supplier = new Supplier(
            "SUP-PO-TEST", "Test Supplier", "orders@example.com");

        factory.Suppliers.Add(supplier);

        return new
        {
            supplierId = supplier.Id,
            warehouseId = factory.Warehouse.Id,
            lines = new[]
            {
                new
                {
                    productId = factory.Product.Id,
                    orderedQuantity = 2,
                    unitPrice = 19.95m
                }
            }
        };
    }

    private static WebApplicationFactory<Program> CreateHost(
        StockFlowApiFactory factory,
        RecordingOrderRepository orders)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPurchaseOrderRepository>();
                services.AddSingleton<IPurchaseOrderRepository>(orders);
            });
        });
    }

    private sealed class RecordingOrderRepository : IPurchaseOrderRepository
    {
        public PurchaseOrder? SavedOrder { get; private set; }
        public int SaveCallCount { get; private set; }
        public Exception? SaveFailure { get; init; }

        public Task<(PurchaseOrder Order, Guid Version)?> GetByIdAsync(
            Guid purchaseOrderId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SaveAsync(
            PurchaseOrder purchaseOrder,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SaveCallCount++;

            if (SaveFailure is { } failure)
            {
                return Task.FromException(failure);
            }

            SavedOrder = purchaseOrder;

            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData("supplierId", "SupplierId")]
    [InlineData("warehouseId", "WarehouseId")]
    [InlineData("lines", "Lines")]
    [InlineData("unitPrice", "Lines[0].UnitPrice")]
    public async Task Create_WithMissingField_ReturnsValidationProblem(
    string field,
    string expectedErrorKey)
    {
        using var factory = new StockFlowApiFactory();
        var orders = new RecordingOrderRepository();
        using var host = CreateHost(factory, orders);
        using var client = await host.CreateClientWithAntiforgeryAsync();

        var request = JsonSerializer.SerializeToNode(CreateRequest(factory))!.AsObject();
        var target = field == "unitPrice"
            ? request["lines"]![0]!.AsObject()
            : request;

        Assert.True(target.Remove(field));

        using var response = await client.PostAsJsonAsync(Endpoint, request);

        await AssertValidationErrorAsync(response, expectedErrorKey);
        Assert.Equal(0, orders.SaveCallCount);
    }

    [Fact]
    public async Task Create_WithExcessPricePrecision_ReturnsIndexedValidationError()
    {
        using var factory = new StockFlowApiFactory();
        var orders = new RecordingOrderRepository();
        using var host = CreateHost(factory, orders);
        using var client = await host.CreateClientWithAntiforgeryAsync();

        var request = JsonSerializer.SerializeToNode(CreateRequest(factory))!.AsObject();
        request["lines"]![0]!["unitPrice"] = 1.001m;

        using var response = await client.PostAsJsonAsync(Endpoint, request);

        await AssertValidationErrorAsync(response, "Lines[0].UnitPrice");
        Assert.Equal(0, orders.SaveCallCount);
    }

    [Fact]
    public async Task Create_WithMissingProduct_ReturnsNotFound()
    {
        using var factory = new StockFlowApiFactory();
        var orders = new RecordingOrderRepository();
        using var host = CreateHost(factory, orders);
        using var client = await host.CreateClientWithAntiforgeryAsync();

        var request = JsonSerializer.SerializeToNode(CreateRequest(factory))!.AsObject();
        request["lines"]![0]!["productId"] = Guid.NewGuid().ToString();

        using var response = await client.PostAsJsonAsync(Endpoint, request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, orders.SaveCallCount);
    }

    [Fact]
    public async Task Create_WhenRepositoryReportsConflict_ReturnsConflictProblem()
    {
        using var factory = new StockFlowApiFactory();

        var orders = new RecordingOrderRepository
        {
            SaveFailure = new PurchaseOrderConflictException(
                Guid.NewGuid(), "The generated order number already exists. Retry creation.")
        };

        using var host = CreateHost(factory, orders);
        using var client = await host.CreateClientWithAntiforgeryAsync();

        using var response = await client.PostAsJsonAsync(
            Endpoint, CreateRequest(factory));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Purchase order conflict", problem.GetProperty("title").GetString());
        Assert.Equal(1, orders.SaveCallCount);
        Assert.Null(orders.SavedOrder);
    }

    private static async Task AssertValidationErrorAsync(
        HttpResponseMessage response,
        string expectedKey)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Contains(
            problem.GetProperty("errors").EnumerateObject(),
            error => string.Equals(error.Name, expectedKey, StringComparison.OrdinalIgnoreCase));
    }
}
