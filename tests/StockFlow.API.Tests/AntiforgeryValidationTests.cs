using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StockFlow.API.Contracts.Authentication;

namespace StockFlow.API.Tests;

public class AntiforgeryValidationTests
{
    private const string HeaderName = "X-CSRF-TOKEN";
    private const string SupplierEndpoint = "/api/suppliers";

    private static HttpClient CreateHttpsClient(
        StockFlowApiFactory factory,
        bool handleCookies = true)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = handleCookies
        });
    }

    private static object SupplierRequest() => new
    {
        code = "DEMO-CSRF",
        name = "[DEMO] CSRF Test Supplier",
        contactEmail = "csrf@example.com"
    };

    private static async Task<AntiforgeryTokenResponse> GetToken(
        HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();

        var token = await response.Content
            .ReadFromJsonAsync<AntiforgeryTokenResponse>();

        Assert.NotNull(token);
        return token;
    }

    [Theory]
    [InlineData("POST", "/api/inventory/receive")]
    [InlineData("POST", "/api/suppliers")]
    [InlineData("PUT", "/api/suppliers/11111111-1111-1111-1111-111111111111")]
    [InlineData("PATCH", "/api/suppliers/11111111-1111-1111-1111-111111111111/activate")]
    [InlineData("PATCH", "/api/suppliers/11111111-1111-1111-1111-111111111111/deactivate")]
    public async Task Mutation_WithoutTokens_IsRejectedBeforeBusinessOperation(
        string method,
        string path)
    {
        using var factory = new StockFlowApiFactory();
        using var client = CreateHttpsClient(factory);
        using var request = new HttpRequestMessage(
            new HttpMethod(method), path);

        if (path == "/api/inventory/receive")
        {
            request.Content = JsonContent.Create(new
            {
                productId = factory.Product.Id,
                warehouseId = factory.Warehouse.Id,
                quantity = 5
            });
        }
        else if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(SupplierRequest());
        }

        using var response = await client.SendAsync(request);

        await AssertRejected(response, factory);
    }

    [Fact]
    public async Task Mutation_WithCookieButNoHeader_IsRejected()
    {
        using var factory = new StockFlowApiFactory();
        using var client = CreateHttpsClient(factory);
        await GetToken(client);

        using var response = await client.PostAsJsonAsync(
            SupplierEndpoint, SupplierRequest());

        await AssertRejected(response, factory);
    }

    [Fact]
    public async Task Mutation_WithHeaderButNoCookie_IsRejected()
    {
        using var factory = new StockFlowApiFactory();
        using var client = CreateHttpsClient(factory, handleCookies: false);
        var token = await GetToken(client);

        client.DefaultRequestHeaders.Add(
            token.HeaderName, token.RequestToken);

        using var response = await client.PostAsJsonAsync(
            SupplierEndpoint, SupplierRequest());

        await AssertRejected(response, factory);
    }

    [Fact]
    public async Task Mutation_WithMismatchedTokenPair_IsRejected()
    {
        using var factory = new StockFlowApiFactory();
        using var firstClient = CreateHttpsClient(factory);
        using var secondClient = CreateHttpsClient(factory);

        var firstToken = await GetToken(firstClient);
        await GetToken(secondClient);

        secondClient.DefaultRequestHeaders.Add(
            firstToken.HeaderName, firstToken.RequestToken);

        using var response = await secondClient.PostAsJsonAsync(
            SupplierEndpoint, SupplierRequest());

        await AssertRejected(response, factory);
    }

    [Fact]
    public async Task Mutation_WithCorruptedRequestToken_IsRejected()
    {
        using var factory = new StockFlowApiFactory();
        using var client = await factory.CreateClientWithAntiforgeryAsync();

        client.DefaultRequestHeaders.Remove(HeaderName);
        client.DefaultRequestHeaders.Add(HeaderName, "invalid-token");

        using var response = await client.PostAsJsonAsync(
            SupplierEndpoint, SupplierRequest());

        await AssertRejected(response, factory);
    }

    [Fact]
    public async Task Mutation_WithTokenForDifferentUser_IsRejected()
    {
        using var factory = new StockFlowApiFactory();
        using var client = await factory.CreateClientWithAntiforgeryAsync();

        // Simulate an identity change after the token was issued.
        factory.User.Id = Guid.NewGuid();

        using var response = await client.PostAsJsonAsync(
            SupplierEndpoint, SupplierRequest());

        await AssertRejected(response, factory);
    }

    [Fact]
    public async Task Mutation_WithValidTokenPair_ReachesBusinessOperation()
    {
        using var factory = new StockFlowApiFactory();
        using var client = await factory.CreateClientWithAntiforgeryAsync();

        using var response = await client.PostAsJsonAsync(
            SupplierEndpoint, SupplierRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, factory.Suppliers.SaveCallCount);
    }

    private static async Task AssertRejected(
        HttpResponseMessage response,
        StockFlowApiFactory factory)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal(0, factory.Suppliers.GetByIdCallCount);
        Assert.Equal(0, factory.Suppliers.SaveCallCount);
        Assert.Equal(0, factory.Inventories.SaveCallCount);
    }
}