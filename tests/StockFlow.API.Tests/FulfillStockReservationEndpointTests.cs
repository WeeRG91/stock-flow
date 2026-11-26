using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.API.Tests;

public sealed class FulfillStockReservationEndpointTests
{
    [Theory]
    [InlineData(StockFlowRoles.Administrator)]
    [InlineData(StockFlowRoles.InventoryManager)]
    [InlineData(StockFlowRoles.WarehouseStaff)]
    public async Task Fulfill_WithAuthorizedRole_ConsumesStockEvenWhenCatalogIsInactive(string role)
    {
        using var factory = new StockFlowApiFactory();
        var (inventory, reservation, version) = Prepare(factory);
        factory.User.Roles = [role];
        factory.Product.Deactivate();
        factory.Warehouse.Deactivate();
        using var client = await factory.CreateClientWithAntiforgeryAsync();
        using var response = await client.PostAsJsonAsync(Url(reservation.Id), new { expectedVersion = version });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
        Assert.Equal(StockReservationStatus.Fulfilled, reservation.Status);
        Assert.Equal(factory.User.Id, reservation.FulfilledByUserId);
        Assert.Equal(factory.OccurredAt, reservation.FulfilledAt);
        Assert.Equal(70, inventory.QuantityOnHand);
        Assert.Equal(20, inventory.QuantityReserved);
        Assert.Equal(50, inventory.QuantityAvailable);
        Assert.Empty(inventory.PendingMovements);
        var movement = Assert.Single(factory.Reservations.LastFulfillmentMovements);
        Assert.Equal(-30, movement.QuantityChange);
        Assert.Equal(StockMovementType.Removal, movement.Type);
        Assert.Equal(inventory.Id, movement.InventoryId);
        Assert.Equal(reservation.FulfilledAt, movement.OccurredAt);
        Assert.Contains(reservation.Id.ToString(), movement.Reason);
        Assert.Same(reservation, factory.Reservations.LastSavedReservation);
        Assert.Same(inventory, factory.Reservations.LastSavedInventory);
        Assert.Equal(1, factory.Reservations.FulfillmentSaveCallCount);
        Assert.Equal(0, factory.Inventories.SaveCallCount);
        Assert.Equal(0, factory.Reservations.SaveCallCount);
    }

    [Theory]
    [InlineData(StockFlowRoles.Viewer)]
    [InlineData(StockFlowRoles.PurchasingOfficer)]
    [InlineData(StockFlowRoles.PurchasingManager)]
    [InlineData("UnknownRole")]
    public async Task Fulfill_WithoutPermission_ReturnsForbiddenBeforeLookup(string role)
    {
        using var factory = new StockFlowApiFactory();
        var (inventory, reservation, version) = Prepare(factory);
        factory.User.Roles = [role];
        using var client = await factory.CreateClientWithAntiforgeryAsync();
        using var response = await client.PostAsJsonAsync(Url(reservation.Id), new { expectedVersion = version });
        await AssertProblem(response, HttpStatusCode.Forbidden);
        Assert.Equal(0, factory.Reservations.LookupByIdCallCount);
        AssertUnchanged(factory, inventory, reservation);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("empty")]
    [InlineData("malformed")]
    public async Task Fulfill_WithInvalidVersion_ReturnsValidationBeforeLookup(string invalid)
    {
        using var factory = new StockFlowApiFactory();
        var (inventory, reservation, _) = Prepare(factory);
        var body = new Dictionary<string, object?>();
        if (invalid != "missing")
            body["ExpectedVersion"] = invalid switch
            {
                "null" => null,
                "empty" => Guid.Empty,
                _ => "not-a-guid"
            };
        using var client = await factory.CreateClientWithAntiforgeryAsync();
        using var response = await client.PostAsJsonAsync(Url(reservation.Id), body);
        var problem = await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.True(problem.GetProperty("errors").EnumerateObject().Any());
        Assert.Equal(0, factory.Reservations.LookupByIdCallCount);
        AssertUnchanged(factory, inventory, reservation);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Fulfill_WithMissingResource_ReturnsNotFound(bool missingReservation)
    {
        using var factory = new StockFlowApiFactory();
        var (inventory, reservation, version) = Prepare(factory);
        if (!missingReservation) factory.Inventories.Inventory = null;
        using var client = await factory.CreateClientWithAntiforgeryAsync();
        using var response = await client.PostAsJsonAsync(
            Url(missingReservation ? Guid.NewGuid() : reservation.Id), new { expectedVersion = version });
        await AssertProblem(response, HttpStatusCode.NotFound);
        AssertUnchanged(factory, inventory, reservation);
    }

    [Fact]
    public async Task Fulfill_WithStaleVersion_ReturnsConflictWithoutLoadingInventory()
    {
        using var factory = new StockFlowApiFactory();
        var (inventory, reservation, _) = Prepare(factory);
        using var client = await factory.CreateClientWithAntiforgeryAsync();
        using var response = await client.PostAsJsonAsync(Url(reservation.Id), new { expectedVersion = Guid.NewGuid() });
        var problem = await AssertProblem(response, HttpStatusCode.Conflict);
        Assert.Equal("Stock reservation conflict", problem.GetProperty("title").GetString());
        Assert.Equal(0, factory.Inventories.GetCallCount);
        AssertUnchanged(factory, inventory, reservation);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Fulfill_WithTerminalReservation_ReturnsConflictWithoutChangingHistory(bool fulfilled)
    {
        using var factory = new StockFlowApiFactory();
        var (inventory, reservation, version) = Prepare(factory);
        var actor = Guid.NewGuid();
        if (fulfilled) reservation.Fulfill(factory.OccurredAt, actor);
        else reservation.Release(factory.OccurredAt, actor);
        using var client = await factory.CreateClientWithAntiforgeryAsync();
        using var response = await client.PostAsJsonAsync(Url(reservation.Id), new { expectedVersion = version });
        await AssertProblem(response, HttpStatusCode.Conflict);
        Assert.Equal(fulfilled ? StockReservationStatus.Fulfilled : StockReservationStatus.Released, reservation.Status);
        Assert.Equal(actor, fulfilled ? reservation.FulfilledByUserId : reservation.ReleasedByUserId);
        Assert.Equal(50, inventory.QuantityReserved);
        Assert.Equal(100, inventory.QuantityOnHand);
        Assert.Empty(inventory.PendingMovements);
        Assert.Equal(0, factory.Reservations.FulfillmentSaveCallCount);
    }

    [Fact]
    public async Task Fulfill_WhenRepeated_DoesNotFulfillTwice()
    {
        using var factory = new StockFlowApiFactory();
        var (inventory, reservation, version) = Prepare(factory);
        using var client = await factory.CreateClientWithAntiforgeryAsync();
        using var first = await client.PostAsJsonAsync(Url(reservation.Id), new { expectedVersion = version });
        using var second = await client.PostAsJsonAsync(Url(reservation.Id), new { expectedVersion = version });
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        await AssertProblem(second, HttpStatusCode.Conflict);
        Assert.Equal(1, factory.Reservations.FulfillmentSaveCallCount);
        Assert.Equal(20, inventory.QuantityReserved);
        Assert.Equal(70, inventory.QuantityOnHand);
        Assert.Empty(inventory.PendingMovements);
        Assert.Single(factory.Reservations.LastFulfillmentMovements);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Fulfill_WhenSaveFails_ReturnsConflictOrSafeServerError(bool conflict)
    {
        using var factory = new StockFlowApiFactory();
        var (inventory, reservation, version) = Prepare(factory);
        const string privateDetail = "Private database connection information";
        factory.Reservations.SaveException = conflict
            ? new StockReservationConflictException(reservation.Id, "Concurrent update; reload before retrying.")
            : new InvalidOperationException(privateDetail);
        using var client = await factory.CreateClientWithAntiforgeryAsync();
        using var response = await client.PostAsJsonAsync(Url(reservation.Id), new { expectedVersion = version });
        var problem = await AssertProblem(response, conflict ? HttpStatusCode.Conflict : HttpStatusCode.InternalServerError);
        Assert.DoesNotContain(privateDetail, problem.GetRawText());
        Assert.Equal(1, factory.Reservations.FulfillmentSaveCallCount);
        Assert.Null(factory.Reservations.LastSavedReservation);
        Assert.Equal(0, factory.Inventories.SaveCallCount);
        Assert.Single(inventory.PendingMovements);
        Assert.Empty(factory.Reservations.LastFulfillmentMovements);
    }

    [Fact]
    public async Task Fulfill_WithoutAntiforgeryToken_DoesNotLookupOrSave()
    {
        using var factory = new StockFlowApiFactory();
        var (inventory, reservation, version) = Prepare(factory);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        using var response = await client.PostAsJsonAsync(Url(reservation.Id), new { expectedVersion = version });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Reservations.LookupByIdCallCount);
        AssertUnchanged(factory, inventory, reservation);
    }

    [Fact]
    public async Task Fulfill_WhenAnonymous_ReturnsUnauthorized()
    {
        using var factory = new StockFlowApiFactory();
        var (inventory, reservation, version) = Prepare(factory);
        factory.User.IsAuthenticated = false;
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Url(reservation.Id), new { expectedVersion = version });
        await AssertProblem(response, HttpStatusCode.Unauthorized);
        Assert.Equal(0, factory.Reservations.LookupByIdCallCount);
        AssertUnchanged(factory, inventory, reservation);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", HttpStatusCode.BadRequest)]
    [InlineData("not-a-guid", HttpStatusCode.NotFound)]
    public async Task Fulfill_WithInvalidRouteId_DoesNotLookup(string id, HttpStatusCode status)
    {
        using var factory = new StockFlowApiFactory();
        var (inventory, reservation, version) = Prepare(factory);
        using var client = await factory.CreateClientWithAntiforgeryAsync();
        using var response = await client.PostAsJsonAsync($"/api/stock-reservations/{id}/fulfill", new { expectedVersion = version });
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(0, factory.Reservations.LookupByIdCallCount);
        AssertUnchanged(factory, inventory, reservation);
    }

    [Fact]
    public async Task Fulfill_WithInconsistentBalance_ReturnsConflictWithoutTransition()
    {
        using var factory = new StockFlowApiFactory();
        var (inventory, reservation, version) = Prepare(factory);
        inventory.ReleaseReservedStock(30);
        using var client = await factory.CreateClientWithAntiforgeryAsync();
        using var response = await client.PostAsJsonAsync(Url(reservation.Id), new { expectedVersion = version });
        await AssertProblem(response, HttpStatusCode.Conflict);
        Assert.Equal(StockReservationStatus.Active, reservation.Status);
        Assert.Null(reservation.FulfilledAt);
        Assert.Equal(20, inventory.QuantityReserved);
        Assert.Equal(0, factory.Reservations.FulfillmentSaveCallCount);
    }

    private static (InventoryEntity Inventory, StockReservation Reservation, Guid Version) Prepare(StockFlowApiFactory factory)
    {
        var inventory = new InventoryEntity(factory.Product.Id, factory.Warehouse.Id);
        inventory.ReceiveStock(100, "Synthetic stock", factory.OccurredAt);
        inventory.ReserveStock(50);
        inventory.AcknowledgePersistedMovements(inventory.PendingMovements.Select(item => item.Id).ToArray());
        factory.Inventories.Inventory = inventory;
        var reservation = new StockReservation(inventory.Id, 30, "Operation", factory.OccurredAt.AddHours(-1), Guid.NewGuid());
        var version = Guid.NewGuid();
        factory.Reservations.AddTracked(reservation, version);
        return (inventory, reservation, version);
    }

    private static string Url(Guid id) => $"/api/stock-reservations/{id}/fulfill";

    private static void AssertUnchanged(StockFlowApiFactory factory, InventoryEntity inventory, StockReservation reservation)
    {
        Assert.Equal(StockReservationStatus.Active, reservation.Status);
        Assert.Null(reservation.FulfilledAt);
        Assert.Null(reservation.FulfilledByUserId);
        Assert.Equal(100, inventory.QuantityOnHand);
        Assert.Equal(50, inventory.QuantityReserved);
        Assert.Empty(inventory.PendingMovements);
        Assert.Equal(0, factory.Reservations.FulfillmentSaveCallCount);
        Assert.Equal(0, factory.Inventories.SaveCallCount);
        Assert.Equal(0, factory.Reservations.SaveCallCount);
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
