using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StockFlow.Application.Authorization;
using StockFlow.Application.StockReservations;
using StockFlow.Domain.Enums;

namespace StockFlow.API.Tests;

public sealed class GetStockReservationByIdEndpointTests
{
    [Theory]
    [InlineData(StockFlowRoles.Administrator)]
    [InlineData(StockFlowRoles.InventoryManager)]
    [InlineData(StockFlowRoles.WarehouseStaff)]
    [InlineData(StockFlowRoles.PurchasingOfficer)]
    [InlineData(StockFlowRoles.PurchasingManager)]
    [InlineData(StockFlowRoles.Viewer)]
    public async Task Get_WithReadRole_ReturnsAllDetailsAndVersion(string role)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = [role];
        var expected = CreateDto(factory, StockReservationStatus.Active);
        factory.ReservationReader.Result = expected;
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(Url(expected.ReservationId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, await response.Content.ReadFromJsonAsync<StockReservationDto>());
        Assert.Equal(expected.ReservationId, factory.ReservationReader.ReceivedId);
        Assert.Equal(1, factory.ReservationReader.CallCount);
        Assert.Equal(0, factory.Reservations.SaveCallCount);
        Assert.Equal(0, factory.Reservations.ReleaseSaveCallCount);
        Assert.Equal(0, factory.Reservations.FulfillmentSaveCallCount);
    }

    [Theory]
    [InlineData(StockReservationStatus.Released)]
    [InlineData(StockReservationStatus.Fulfilled)]
    public async Task Get_AsViewer_ReturnsTerminalAuditHistory(StockReservationStatus status)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = [StockFlowRoles.Viewer];
        var expected = CreateDto(factory, status);
        factory.ReservationReader.Result = expected;
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(Url(expected.ReservationId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, await response.Content.ReadFromJsonAsync<StockReservationDto>());
    }

    [Fact]
    public async Task Get_WhenMissing_ReturnsNotFoundProblem()
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(Url(Guid.NewGuid()));
        await AssertProblem(response, HttpStatusCode.NotFound);
        Assert.Equal(1, factory.ReservationReader.CallCount);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", HttpStatusCode.BadRequest)]
    [InlineData("not-a-guid", HttpStatusCode.NotFound)]
    public async Task Get_WithInvalidId_DoesNotRead(string id, HttpStatusCode status)
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/stock-reservations/{id}");
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(0, factory.ReservationReader.CallCount);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task Get_WithoutAccess_DoesNotRead(bool authenticated, HttpStatusCode status)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.IsAuthenticated = authenticated;
        factory.User.Roles = ["UnknownRole"];
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(Url(Guid.NewGuid()));
        await AssertProblem(response, status);
        Assert.Equal(0, factory.ReservationReader.CallCount);
    }

    [Fact]
    public async Task Get_WhenReaderFails_ReturnsSafeServerProblem()
    {
        using var factory = new StockFlowApiFactory();
        const string privateDetail = "Private database connection information";
        factory.ReservationReader.ReadException = new InvalidOperationException(privateDetail);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(Url(Guid.NewGuid()));
        var problem = await AssertProblem(response, HttpStatusCode.InternalServerError);
        Assert.DoesNotContain(privateDetail, problem.GetRawText());
    }

    private static string Url(Guid id) => $"/api/stock-reservations/{id}";
    private static StockReservationDto CreateDto(StockFlowApiFactory factory, StockReservationStatus status) =>
        new(Guid.NewGuid(), Guid.NewGuid(), factory.Product.Id, factory.Product.Name, factory.Product.Sku,
            factory.Warehouse.Id, factory.Warehouse.Name, factory.Warehouse.Code, 30, "Operation", status,
            factory.OccurredAt.AddHours(-1), factory.User.Id,
            status == StockReservationStatus.Released ? factory.OccurredAt : null,
            status == StockReservationStatus.Released ? factory.User.Id : null,
            status == StockReservationStatus.Fulfilled ? factory.OccurredAt : null,
            status == StockReservationStatus.Fulfilled ? factory.User.Id : null, Guid.NewGuid());

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
