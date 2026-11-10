using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StockFlow.API.Contracts.Authentication;

namespace StockFlow.API.Tests;

public static class TestClientExtensions
{
    public static async Task<HttpClient> CreateClientWithAntiforgeryAsync(
        this WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = true
            });

        try
        {
            using var response = await client.GetAsync("/api/auth/csrf");
            response.EnsureSuccessStatusCode();

            var token = await response.Content
                .ReadFromJsonAsync<AntiforgeryTokenResponse>();

            if (token is null ||
                string.IsNullOrWhiteSpace(token.RequestToken) ||
                string.IsNullOrWhiteSpace(token.HeaderName))
            {
                throw new InvalidOperationException(
                    "The test client could not obtain an antiforgery token.");
            }

            client.DefaultRequestHeaders.Add(
                token.HeaderName,
                token.RequestToken);

            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }
}