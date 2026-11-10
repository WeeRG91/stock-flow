using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StockFlow.API.Contracts.Authentication;

namespace StockFlow.API.Tests;

public class AntiforgeryEndpointTests
{
    private const string Endpoint = "/api/auth/csrf";
    private const string CookieName = "__Host-StockFlow.Csrf";

    private static HttpClient CreateHttpsClient(StockFlowApiFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetToken_IsAvailableAndNotCacheable(bool authenticated)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.IsAuthenticated = authenticated;
        using var client = CreateHttpsClient(factory);

        using var response = await client.GetAsync(Endpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);

        var result = await response.Content
            .ReadFromJsonAsync<AntiforgeryTokenResponse>();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.RequestToken));
        Assert.Equal("X-CSRF-TOKEN", result.HeaderName);
    }

    [Fact]
    public async Task GetToken_SetsSecureHttpOnlyHostCookie()
    {
        using var factory = new StockFlowApiFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.GetAsync(Endpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(CookieName + "="));

        var normalized = cookie.ToLowerInvariant();

        Assert.Contains("; path=/", normalized);
        Assert.Contains("; secure", normalized);
        Assert.Contains("; httponly", normalized);
        Assert.Contains("; samesite=lax", normalized);
        Assert.DoesNotContain("domain=", normalized);

        Assert.DoesNotContain(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-StockFlow.Auth="));
    }

    [Fact]
    public async Task GetToken_ReturnsConfiguredHeaderName()
    {
        using var factory = new StockFlowApiFactory();
        using var client = CreateHttpsClient(factory);

        using var response = await client.GetAsync(Endpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<AntiforgeryTokenResponse>();

        Assert.NotNull(result);

        var options = factory.Services
            .GetRequiredService<IOptions<AntiforgeryOptions>>().Value;

        Assert.Equal(options.HeaderName, result.HeaderName);
    }

    [Fact]
    public async Task GetToken_ReusesExistingCookie()
    {
        using var factory = new StockFlowApiFactory();
        using var client = CreateHttpsClient(factory);

        using var first = await client.GetAsync(Endpoint);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Contains(first.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(CookieName + "="));

        using var second = await client.GetAsync(Endpoint);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var cookies = second.Headers.TryGetValues("Set-Cookie", out var values)
            ? values
            : Array.Empty<string>();

        Assert.DoesNotContain(cookies,
            value => value.StartsWith(CookieName + "="));
    }

    [Fact]
    public async Task GetToken_DoesNotAuthenticateAnonymousClient()
    {
        using var factory = new StockFlowApiFactory
        {
            UseTestAuthentication = false
        };
        using var client = CreateHttpsClient(factory);

        using var tokenResponse = await client.GetAsync(Endpoint);
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);

        using var protectedResponse = await client.GetAsync("/api/suppliers");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            protectedResponse.StatusCode);
    }
}