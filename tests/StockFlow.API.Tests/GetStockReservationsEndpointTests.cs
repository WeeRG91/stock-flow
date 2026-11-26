using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StockFlow.Application.Authorization;
using StockFlow.Application.StockReservations;
using StockFlow.Application.StockReservations.GetStockReservations;
using StockFlow.Domain.Enums;

namespace StockFlow.API.Tests;

public sealed class GetStockReservationsEndpointTests
{
    private const string Endpoint = "/api/stock-reservations";

    [Theory]
    [InlineData(StockFlowRoles.Administrator)]
    [InlineData(StockFlowRoles.InventoryManager)]
    [InlineData(StockFlowRoles.WarehouseStaff)]
    [InlineData(StockFlowRoles.PurchasingOfficer)]
    [InlineData(StockFlowRoles.PurchasingManager)]
    [InlineData(StockFlowRoles.Viewer)]
    public async Task GetPage_WithReadRole_ReturnsDtosAndMetadata(string role)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = [role];
        var item = new StockReservationDto(Guid.NewGuid(), Guid.NewGuid(), factory.Product.Id,
            factory.Product.Name, factory.Product.Sku, factory.Warehouse.Id, factory.Warehouse.Name,
            factory.Warehouse.Code, 30, "Operation", StockReservationStatus.Active,
            factory.OccurredAt, factory.User.Id, null, null, null, null, Guid.NewGuid());
        factory.ReservationReader.Items = new[] { item };
        factory.ReservationReader.TotalCount = 3;
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"{Endpoint}?pageNumber=2&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<GetStockReservationsResult>();
        Assert.NotNull(result);
        Assert.Equal(item, Assert.Single(result.Items));
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(1, result.PageSize);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, factory.ReservationReader.ReceivedPageNumber);
        Assert.Equal(1, factory.ReservationReader.ReceivedPageSize);
        Assert.Equal(1, factory.ReservationReader.PageCallCount);
        Assert.Equal(0, factory.ReservationReader.CallCount);
        AssertNoWrites(factory);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public async Task GetPage_ForwardsStatusInventoryAndTrimmedSearch(int? status, bool filterInventory)
    {
        using var factory = new StockFlowApiFactory();
        Guid? inventoryId = filterInventory ? Guid.NewGuid() : null;
        var url = $"{Endpoint}?pageNumber=2&pageSize=10&search={Uri.EscapeDataString("  Operation A  ")}";
        if (status.HasValue) url += $"&status={status.Value}";
        if (inventoryId.HasValue) url += $"&inventoryId={inventoryId.Value}";
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(status.HasValue ? (StockReservationStatus?)status.Value : null,
            factory.ReservationReader.ReceivedStatus);
        Assert.Equal(inventoryId, factory.ReservationReader.ReceivedInventoryId);
        Assert.Equal("Operation A", factory.ReservationReader.ReceivedSearch);
        Assert.Equal(1, factory.ReservationReader.PageCallCount);
        AssertNoWrites(factory);
    }

    [Theory]
    [InlineData("pageNumber=0")]
    [InlineData("pageNumber=-1")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("pageNumber=abc")]
    [InlineData("pageSize=abc")]
    [InlineData("pageNumber=2147483647&pageSize=100")]
    [InlineData("status=0")]
    [InlineData("status=99")]
    [InlineData("status=unknown")]
    [InlineData("inventoryId=00000000-0000-0000-0000-000000000000")]
    [InlineData("inventoryId=not-a-guid")]
    public async Task GetPage_WithInvalidParameters_ReturnsValidationBeforeReading(string parameters)
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"{Endpoint}?{parameters}");
        var problem = await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.True(problem.GetProperty("errors").EnumerateObject().Any());
        Assert.Equal(0, factory.ReservationReader.PageCallCount);
        AssertNoWrites(factory);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(9, 4)]
    public async Task GetPage_WithEmptyPage_PreservesMetadata(int page, int totalCount)
    {
        using var factory = new StockFlowApiFactory();
        factory.ReservationReader.TotalCount = totalCount;
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(page == 1 ? Endpoint : $"{Endpoint}?pageNumber={page}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<GetStockReservationsResult>();
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(page, result.PageNumber);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(totalCount, result.TotalCount);
        Assert.Null(factory.ReservationReader.ReceivedStatus);
        Assert.Null(factory.ReservationReader.ReceivedSearch);
        Assert.Null(factory.ReservationReader.ReceivedInventoryId);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task GetPage_WithoutAccess_DoesNotRead(bool authenticated, HttpStatusCode status)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.IsAuthenticated = authenticated;
        factory.User.Roles = ["UnknownRole"];
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(Endpoint);
        await AssertProblem(response, status);
        Assert.Equal(0, factory.ReservationReader.PageCallCount);
        AssertNoWrites(factory);
    }

    [Fact]
    public async Task GetPage_WhenReaderFails_ReturnsSafeServerProblem()
    {
        using var factory = new StockFlowApiFactory();
        const string privateDetail = "Private database connection information";
        factory.ReservationReader.ReadException = new InvalidOperationException(privateDetail);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(Endpoint);
        var problem = await AssertProblem(response, HttpStatusCode.InternalServerError);
        Assert.DoesNotContain(privateDetail, problem.GetRawText());
        Assert.Equal(1, factory.ReservationReader.PageCallCount);
    }

    private static void AssertNoWrites(StockFlowApiFactory factory)
    {
        Assert.Equal(0, factory.Reservations.SaveCallCount);
        Assert.Equal(0, factory.Reservations.ReleaseSaveCallCount);
        Assert.Equal(0, factory.Reservations.FulfillmentSaveCallCount);
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
