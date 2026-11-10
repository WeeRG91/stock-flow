using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StockFlow.Application.Authorization;
using StockFlow.Application.Suppliers;

namespace StockFlow.API.Tests;

public class EndpointAuthorizationTests
{
    private const string SupplierId =
        "11111111-1111-1111-1111-111111111111";

    public static IEnumerable<object[]> ProtectedEndpoints()
    {
        yield return ["GET", "/api/products"];
        yield return ["GET", "/api/warehouses"];
        yield return ["GET", "/api/inventory"];
        yield return ["GET", "/api/suppliers"];
        yield return ["GET", $"/api/suppliers/{SupplierId}"];
        yield return ["POST", "/api/inventory/receive"];
        yield return ["POST", "/api/suppliers"];
        yield return ["PUT", $"/api/suppliers/{SupplierId}"];
        yield return ["PATCH", $"/api/suppliers/{SupplierId}/activate"];
        yield return ["PATCH", $"/api/suppliers/{SupplierId}/deactivate"];
        yield return ["POST", "/hubs/inventory/negotiate?negotiateVersion=1"];
    }

    public static IEnumerable<object[]> MutationEndpoints() =>
        ProtectedEndpoints().Where(endpoint =>
            (string)endpoint[0] != "GET" &&
            ((string)endpoint[1]).StartsWith("/api/"));

    private static HttpRequestMessage CreateRequest(
        StockFlowApiFactory factory,
        string method,
        string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);

        if (path == "/api/inventory/receive")
        {
            request.Content = JsonContent.Create(new
            {
                productId = factory.Product.Id,
                warehouseId = factory.Warehouse.Id,
                quantity = 5
            });
        }
        else if (method == "POST" && path == "/api/suppliers")
        {
            request.Content = JsonContent.Create(new
            {
                code = "DEMO-AUTH",
                name = "[DEMO] Authorization Supplier",
                contactEmail = "auth@example.com"
            });
        }
        else if (method == "PUT")
        {
            request.Content = JsonContent.Create(new
            {
                name = "[DEMO] Updated Supplier",
                contactEmail = "updated@example.com"
            });
        }

        return request;
    }

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task AnonymousRequest_ReturnsUnauthorized(
        string method,
        string path)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.IsAuthenticated = false;
        using var client = await factory.CreateClientWithAntiforgeryAsync();
        using var request = CreateRequest(factory, method, path);

        using var response = await client.SendAsync(request);

        await AssertProblem(response, HttpStatusCode.Unauthorized);
        Assert.Equal(0, factory.Suppliers.SaveCallCount);
    }

    [Theory]
    [MemberData(nameof(MutationEndpoints))]
    public async Task Viewer_CannotMutateBusinessData(
        string method,
        string path)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = [StockFlowRoles.Viewer];
        using var client = await factory.CreateClientWithAntiforgeryAsync();
        using var request = CreateRequest(factory, method, path);

        using var response = await client.SendAsync(request);

        await AssertProblem(response, HttpStatusCode.Forbidden);
        Assert.Equal(0, factory.Suppliers.SaveCallCount);
    }

    [Theory]
    [InlineData("/api/products")]
    [InlineData("/api/warehouses")]
    [InlineData("/api/inventory")]
    [InlineData("/api/suppliers")]
    [InlineData("/api/suppliers/" + SupplierId)]
    public async Task Viewer_CanReadBusinessData(string path)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = [StockFlowRoles.Viewer];

        factory.SupplierReader.SupplierByIdResult = new SupplierDto(
            Guid.Parse(SupplierId),
            "DEMO-OFFICE",
            "[DEMO] Office Supplies",
            "office@example.com",
            null,
            true);

        using var client = await factory.CreateClientWithAntiforgeryAsync();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/products")]
    [InlineData("/api/warehouses")]
    [InlineData("/api/inventory")]
    [InlineData("/api/suppliers")]
    public async Task UnknownRole_CannotReadBusinessData(string path)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = ["UnknownRole"];
        using var client = await factory.CreateClientWithAntiforgeryAsync();

        using var response = await client.GetAsync(path);

        await AssertProblem(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task WarehouseStaff_CanReceiveStock()
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = [StockFlowRoles.WarehouseStaff];
        using var client = await factory.CreateClientWithAntiforgeryAsync();
        using var request = CreateRequest(
            factory, "POST", "/api/inventory/receive");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PurchasingOfficer_CanCreateSupplier()
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = [StockFlowRoles.PurchasingOfficer];
        using var client = await factory.CreateClientWithAntiforgeryAsync();
        using var request = CreateRequest(
            factory, "POST", "/api/suppliers");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, factory.Suppliers.SaveCallCount);
    }

    private static async Task AssertProblem(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Headers.Location);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal((int)expectedStatus,
            problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(
            problem.GetProperty("traceId").GetString()));
    }
}