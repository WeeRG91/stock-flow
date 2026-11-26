using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Application.StockReservations.CreateStockReservation;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.API.Tests;

public sealed class CreateStockReservationEndpointTests
{
    private const string Endpoint = "/api/stock-reservations";

    [Theory]
    [InlineData(StockFlowRoles.Administrator)]
    [InlineData(StockFlowRoles.InventoryManager)]
    [InlineData(StockFlowRoles.WarehouseStaff)]
    public async Task Create_WithAuthorizedRole_AllocatesAndReturnsReservation(string role)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = [role];
        var inventory = PrepareInventory(factory);
        using var client = await factory.CreateClientWithAntiforgeryAsync();

        using var response = await client.PostAsJsonAsync(Endpoint, Request(inventory));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateStockReservationResult>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.ReservationId);
        Assert.Equal(factory.Reservations.LastSavedReservation!.Id, result.ReservationId);
        Assert.Equal(inventory.Id, result.InventoryId);
        Assert.Equal(30, result.Quantity);
        Assert.Equal("Operation A", result.Reference);
        Assert.Equal(StockReservationStatus.Active, result.Status);
        Assert.Equal(factory.User.Id, result.CreatedByUserId);
        Assert.Equal(factory.OccurredAt, result.CreatedAt);
        Assert.Same(inventory, factory.Reservations.LastSavedInventory);
        Assert.Equal(100, inventory.QuantityOnHand);
        Assert.Equal(50, inventory.QuantityReserved);
        Assert.Equal(50, inventory.QuantityAvailable);
        Assert.Empty(inventory.PendingMovements);
        Assert.Equal(1, factory.Reservations.SaveCallCount);
        Assert.Equal(0, factory.Inventories.SaveCallCount);
    }

    [Theory]
    [InlineData("RequestId", "missing")]
    [InlineData("InventoryId", "missing")]
    [InlineData("Quantity", "missing")]
    [InlineData("Reference", "missing")]
    [InlineData("RequestId", "emptyGuid")]
    [InlineData("InventoryId", "emptyGuid")]
    [InlineData("RequestId", "malformed")]
    [InlineData("InventoryId", "malformed")]
    [InlineData("Quantity", "zero")]
    [InlineData("Quantity", "negative")]
    [InlineData("Quantity", "overflow")]
    [InlineData("Reference", "empty")]
    [InlineData("Reference", "whitespace")]
    public async Task Create_WithInvalidRequest_ReturnsValidationBeforeReading(string field, string invalid)
    {
        using var factory = new StockFlowApiFactory();
        var inventory = PrepareInventory(factory);
        var request = Request(inventory);
        if (invalid == "missing") request.Remove(field);
        else request[field] = invalid switch
        {
            "emptyGuid" => Guid.Empty,
            "malformed" => "not-a-guid",
            "zero" => 0,
            "negative" => -1,
            "overflow" => (long)int.MaxValue + 1,
            "empty" => "",
            _ => " \t\r\n"
        };
        using var client = await factory.CreateClientWithAntiforgeryAsync();

        using var response = await client.PostAsJsonAsync(Endpoint, request);

        var problem = await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.True(problem.GetProperty("errors").EnumerateObject().Any());
        Assert.Equal(0, factory.Reservations.GetCallCount);
        Assert.Equal(0, factory.Inventories.GetCallCount);
        AssertUnchanged(factory, inventory);
    }

    [Theory]
    [InlineData(StockFlowRoles.Viewer)]
    [InlineData(StockFlowRoles.PurchasingOfficer)]
    [InlineData(StockFlowRoles.PurchasingManager)]
    [InlineData("UnknownRole")]
    public async Task Create_WithoutPermission_ReturnsForbidden(string role)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = [role];
        var inventory = PrepareInventory(factory);
        using var client = await factory.CreateClientWithAntiforgeryAsync();
        using var response = await client.PostAsJsonAsync(Endpoint, Request(inventory));
        await AssertProblem(response, HttpStatusCode.Forbidden);
        Assert.Equal(0, factory.Reservations.GetCallCount);
        AssertUnchanged(factory, inventory);
    }

    [Fact]
    public async Task Create_WhenAnonymous_ReturnsUnauthorized()
    {
        using var factory = new StockFlowApiFactory();
        factory.User.IsAuthenticated = false;
        var inventory = PrepareInventory(factory);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Endpoint, Request(inventory));
        await AssertProblem(response, HttpStatusCode.Unauthorized);
        Assert.Equal(0, factory.Reservations.GetCallCount);
        AssertUnchanged(factory, inventory);
    }

    [Fact]
    public async Task Create_WithoutAntiforgeryToken_DoesNotReadOrSave()
    {
        using var factory = new StockFlowApiFactory();
        var inventory = PrepareInventory(factory);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.PostAsJsonAsync(Endpoint, Request(inventory));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Reservations.GetCallCount);
        AssertUnchanged(factory, inventory);
    }

    [Theory]
    [InlineData("Inventory")]
    [InlineData("Product")]
    [InlineData("Warehouse")]
    public async Task Create_WithMissingResource_ReturnsNotFound(string missing)
    {
        using var factory = new StockFlowApiFactory();
        var inventory = PrepareInventory(factory);
        if (missing == "Inventory") factory.Inventories.Inventory = null;
        if (missing == "Product") factory.Products.Product = null;
        if (missing == "Warehouse") factory.Warehouses.Warehouse = null;
        using var client = await factory.CreateClientWithAntiforgeryAsync();
        using var response = await client.PostAsJsonAsync(Endpoint, Request(inventory));
        await AssertProblem(response, HttpStatusCode.NotFound);
        AssertUnchanged(factory, inventory);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Create_WithInactiveResource_ReturnsConflictWithoutAllocation(bool product)
    {
        using var factory = new StockFlowApiFactory();
        var inventory = PrepareInventory(factory);
        if (product) factory.Product.Deactivate();
        else factory.Warehouse.Deactivate();
        using var client = await factory.CreateClientWithAntiforgeryAsync();
        using var response = await client.PostAsJsonAsync(Endpoint, Request(inventory));
        await AssertProblem(response, HttpStatusCode.Conflict);
        AssertUnchanged(factory, inventory);
    }

    [Fact]
    public async Task Create_WithInsufficientAvailableStock_ReturnsConflict()
    {
        using var factory = new StockFlowApiFactory();
        var inventory = PrepareInventory(factory);
        var request = Request(inventory);
        request["Quantity"] = 81;
        using var client = await factory.CreateClientWithAntiforgeryAsync();
        using var response = await client.PostAsJsonAsync(Endpoint, request);
        await AssertProblem(response, HttpStatusCode.Conflict);
        AssertUnchanged(factory, inventory);
    }

    [Fact]
    public async Task Create_WhenRetried_ReturnsSameReservationWithoutSecondAllocation()
    {
        using var factory = new StockFlowApiFactory();
        var inventory = PrepareInventory(factory);
        var request = Request(inventory);
        using var client = await factory.CreateClientWithAntiforgeryAsync();
        using var first = await client.PostAsJsonAsync(Endpoint, request);
        using var second = await client.PostAsJsonAsync(Endpoint, request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(await first.Content.ReadFromJsonAsync<CreateStockReservationResult>(),
            await second.Content.ReadFromJsonAsync<CreateStockReservationResult>());
        Assert.Equal(1, factory.Reservations.SaveCallCount);
        Assert.Equal(50, inventory.QuantityReserved);
        Assert.Equal(100, inventory.QuantityOnHand);
        Assert.Empty(inventory.PendingMovements);
    }

    [Theory]
    [InlineData("creator")]
    [InlineData("inventory")]
    [InlineData("quantity")]
    [InlineData("reference")]
    public async Task Create_WithMismatchedRequestId_ReturnsReservationConflict(string mismatch)
    {
        using var factory = new StockFlowApiFactory();
        var inventory = PrepareInventory(factory);
        var request = Request(inventory);
        var reservation = new StockReservation(
            mismatch == "inventory" ? Guid.NewGuid() : inventory.Id,
            mismatch == "quantity" ? 31 : 30,
            mismatch == "reference" ? "Other purpose" : "Operation A",
            factory.OccurredAt,
            mismatch == "creator" ? Guid.NewGuid() : factory.User.Id);
        factory.Reservations.Add((Guid)request["RequestId"]!, reservation);
        using var client = await factory.CreateClientWithAntiforgeryAsync();
        using var response = await client.PostAsJsonAsync(Endpoint, request);
        var problem = await AssertProblem(response, HttpStatusCode.Conflict);
        Assert.Equal("Stock reservation conflict", problem.GetProperty("title").GetString());
        Assert.Equal(0, factory.Inventories.GetCallCount);
        AssertUnchanged(factory, inventory);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Create_WhenSaveFails_ReturnsConflictOrSafeServerError(bool conflict)
    {
        using var factory = new StockFlowApiFactory();
        var inventory = PrepareInventory(factory);
        const string privateDetail = "Private database connection information";
        factory.Reservations.SaveException = conflict
            ? new StockReservationConflictException(Guid.NewGuid(), "Inventory changed; reload and try again.")
            : new InvalidOperationException(privateDetail);
        using var client = await factory.CreateClientWithAntiforgeryAsync();
        using var response = await client.PostAsJsonAsync(Endpoint, Request(inventory));
        var problem = await AssertProblem(response,
            conflict ? HttpStatusCode.Conflict : HttpStatusCode.InternalServerError);
        Assert.DoesNotContain(privateDetail, problem.GetRawText());
        if (conflict)
            Assert.Equal("Stock reservation conflict", problem.GetProperty("title").GetString());
        Assert.Equal(1, factory.Reservations.SaveCallCount);
        Assert.Null(factory.Reservations.LastSavedReservation);
        Assert.Equal(0, factory.Inventories.SaveCallCount);
        Assert.Empty(inventory.PendingMovements);
    }

    private static InventoryEntity PrepareInventory(StockFlowApiFactory factory)
    {
        var inventory = new InventoryEntity(factory.Product.Id, factory.Warehouse.Id);
        inventory.ReceiveStock(100, "Synthetic initial stock", factory.OccurredAt);
        inventory.ReserveStock(20);
        inventory.AcknowledgePersistedMovements(inventory.PendingMovements.Select(item => item.Id).ToArray());
        factory.Inventories.Inventory = inventory;
        return inventory;
    }

    private static Dictionary<string, object?> Request(InventoryEntity inventory) => new()
    {
        ["RequestId"] = Guid.NewGuid(), ["InventoryId"] = inventory.Id,
        ["Quantity"] = 30, ["Reference"] = "  Operation A  "
    };

    private static void AssertUnchanged(StockFlowApiFactory factory, InventoryEntity inventory)
    {
        Assert.Equal(100, inventory.QuantityOnHand);
        Assert.Equal(20, inventory.QuantityReserved);
        Assert.Equal(80, inventory.QuantityAvailable);
        Assert.Empty(inventory.PendingMovements);
        Assert.Equal(0, factory.Reservations.SaveCallCount);
        Assert.Equal(0, factory.Inventories.SaveCallCount);
    }

    private static async Task<JsonElement> AssertProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        return problem;
    }
}
