using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StockFlow.Application.Suppliers;
using StockFlow.Domain.Entities;

namespace StockFlow.API.Tests;

public class SupplierLifecycleEndpointTests
{
    private static string Endpoint(Guid id, bool activate) =>
        $"/api/suppliers/{id}/{(activate ? "activate" : "deactivate")}";

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Patch_SetsStatusAndPreservesDetails_WhenRepeated(
        bool activate,
        bool initiallyActive)
    {
        using var factory = new StockFlowApiFactory();
        var supplier = new Supplier(
            "DEMO-OFFICE", "[DEMO] Office Supplies",
            "office@example.com", "123");
        var id = supplier.Id;

        if (!initiallyActive)
            supplier.Deactivate();

        factory.Suppliers.Add(supplier);
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var response = await client.PatchAsync(
                Endpoint(id, activate), null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content
                .ReadFromJsonAsync<SupplierDto>();

            Assert.NotNull(result);
            Assert.Equal(id, result.Id);
            Assert.Equal("DEMO-OFFICE", result.Code);
            Assert.Equal("[DEMO] Office Supplies", result.Name);
            Assert.Equal("office@example.com", result.ContactEmail);
            Assert.Equal("123", result.PhoneNumber);
            Assert.Equal(activate, result.IsActive);
            Assert.Equal(activate, supplier.IsActive);
        }

        Assert.Same(supplier, factory.Suppliers.LastSavedSupplier);
        Assert.Equal(2, factory.Suppliers.GetByIdCallCount);
        Assert.Equal(2, factory.Suppliers.SaveCallCount);
        Assert.Equal(0, factory.Suppliers.ExistsCallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Patch_WhenMissing_ReturnsNotFound(bool activate)
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PatchAsync(
            Endpoint(Guid.NewGuid(), activate), null);

        var problem = await AssertProblem(
            response, HttpStatusCode.NotFound);

        Assert.Equal("Resource not found",
            problem.GetProperty("title").GetString());
        Assert.Equal(1, factory.Suppliers.GetByIdCallCount);
        Assert.Equal(0, factory.Suppliers.SaveCallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Patch_WithEmptyId_ReturnsBadRequest(bool activate)
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PatchAsync(
            Endpoint(Guid.Empty, activate), null);

        var problem = await AssertProblem(
            response, HttpStatusCode.BadRequest);

        Assert.True(problem.GetProperty("errors")
            .TryGetProperty("SupplierId", out _));
        Assert.Equal(0, factory.Suppliers.GetByIdCallCount);
        Assert.Equal(0, factory.Suppliers.SaveCallCount);
    }

    [Theory]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task Patch_WithMalformedId_DoesNotMatchRoute(string action)
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PatchAsync(
            $"/api/suppliers/not-a-guid/{action}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, factory.Suppliers.GetByIdCallCount);
        Assert.Equal(0, factory.Suppliers.SaveCallCount);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Patch_WhenRepositoryFails_ReturnsSafeServerProblem(
        bool activate,
        bool duringSave)
    {
        using var factory = new StockFlowApiFactory();
        var supplier = new Supplier(
            "DEMO-OFFICE", "[DEMO] Office Supplies", "office@example.com");
        factory.Suppliers.Add(supplier);

        const string sensitiveDetail = "Private persistence information";
        var failure = new InvalidOperationException(sensitiveDetail);

        if (duringSave)
            factory.Suppliers.SaveException = failure;
        else
            factory.Suppliers.LookupException = failure;

        using var client = factory.CreateClient();
        using var response = await client.PatchAsync(
            Endpoint(supplier.Id, activate), null);

        var problem = await AssertProblem(
            response, HttpStatusCode.InternalServerError);

        Assert.DoesNotContain(sensitiveDetail, problem.GetRawText());
        Assert.False(string.IsNullOrWhiteSpace(
            problem.GetProperty("traceId").GetString()));
        Assert.Equal(duringSave ? 1 : 0, factory.Suppliers.SaveCallCount);
        Assert.Null(factory.Suppliers.LastSavedSupplier);
    }

    private static async Task<JsonElement> AssertProblem(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal((int)expectedStatus,
            problem.GetProperty("status").GetInt32());

        return problem;
    }
}