using System.Net;
using System.Net.Http.Json;
using StockFlow.Application.Warehouses.GetWarehouses;

namespace StockFlow.API.Tests;

public class GetWarehouseByIdEndpointTests
{
    [Fact]
    public async Task GetById_WhenMissing_ReturnsNotFoundProblem()
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            $"/api/warehouses/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content
            .ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
        Assert.Equal("Resource not found", problem.Title);
        Assert.Equal(1, factory.WarehouseReader.CallCount);
    }

    [Fact]
    public async Task GetById_WithEmptyId_ReturnsValidationProblem()
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            $"/api/warehouses/{Guid.Empty}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content
            .ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.True(problem.Errors.ContainsKey("WarehouseId"));
        Assert.NotEmpty(problem.Errors["WarehouseId"]);
        Assert.Equal(0, factory.WarehouseReader.CallCount);
    }

    [Fact]
    public async Task GetById_WithMalformedId_DoesNotMatchRoute()
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/warehouses/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, factory.WarehouseReader.CallCount);
    }

    [Fact]
    public async Task GetById_WhenAnonymous_ReturnsUnauthorized()
    {
        using var factory = new StockFlowApiFactory();
        factory.User.IsAuthenticated = false;
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            $"/api/warehouses/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, factory.WarehouseReader.CallCount);
    }

    [Fact]
    public async Task GetById_WithoutCatalogPermission_ReturnsForbidden()
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = Array.Empty<string>();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            $"/api/warehouses/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, factory.WarehouseReader.CallCount);
    }

    [Fact]
    public async Task GetById_WhenReaderFails_ReturnsSafeServerProblem()
    {
        using var factory = new StockFlowApiFactory();
        const string sensitiveDetail = "Private database connection information";
        factory.WarehouseReader.ReadException =
            new InvalidOperationException(sensitiveDetail);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            $"/api/warehouses/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content
            .ReadFromJsonAsync<System.Text.Json.JsonElement>();

        Assert.Equal(500, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(
            problem.GetProperty("traceId").GetString()));
        Assert.DoesNotContain(sensitiveDetail, problem.GetRawText());
        Assert.Equal(1, factory.WarehouseReader.CallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetById_ReturnsWarehouseIncludingInactive(
        bool isActive)
    {
        using var factory = new StockFlowApiFactory();

        var warehouse = new WarehouseDto(
            Guid.NewGuid(),
            "TEST-BRU",
            "Synthetic warehouse",
            "Brussels, Belgium",
            isActive);

        factory.WarehouseReader.Warehouses = new[] { warehouse };

        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            $"/api/warehouses/{warehouse.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<WarehouseDto>();

        Assert.Equal(warehouse, result);
        Assert.Equal(1, factory.WarehouseReader.CallCount);
    }
}
