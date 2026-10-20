using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StockFlow.Application.Suppliers;
using StockFlow.Application.Suppliers.CreateSupplier;

namespace StockFlow.API.Tests;

public class GetSupplierByIdEndpointTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetById_ReturnsSupplierIncludingInactive(bool isActive)
    {
        using var factory = new StockFlowApiFactory();

        var supplier = new SupplierDto(
            Guid.NewGuid(), "DEMO-OFFICE", "[DEMO] Office Supplies",
            "office@example.com", null, isActive);

        factory.SupplierReader.SupplierByIdResult = supplier;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/suppliers/{supplier.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<SupplierDto>();

        Assert.Equal(supplier, result);
        Assert.Equal(supplier.Id, factory.SupplierReader.ReceivedSupplierId);
        Assert.Equal(1, factory.SupplierReader.ByIdCallCount);
        Assert.Equal(0, factory.SupplierReader.CallCount);
    }

    [Fact]
    public async Task GetById_WhenMissing_ReturnsNotFoundProblem()
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();
        var id = Guid.NewGuid();

        using var response = await client.GetAsync($"/api/suppliers/{id}");

        var problem = await AssertProblem(response, HttpStatusCode.NotFound);

        Assert.Equal("Resource not found",
            problem.GetProperty("title").GetString());
        Assert.Equal(id, factory.SupplierReader.ReceivedSupplierId);
        Assert.Equal(1, factory.SupplierReader.ByIdCallCount);
    }

    [Fact]
    public async Task GetById_WithEmptyId_ReturnsValidationProblem()
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/suppliers/{Guid.Empty}");

        var problem = await AssertProblem(response, HttpStatusCode.BadRequest);

        Assert.True(problem.GetProperty("errors")
            .TryGetProperty("SupplierId", out var errors));
        Assert.NotEmpty(errors.EnumerateArray());
        Assert.Equal(0, factory.SupplierReader.ByIdCallCount);
    }

    [Fact]
    public async Task GetById_WithMalformedId_DoesNotMatchRoute()
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/suppliers/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, factory.SupplierReader.ByIdCallCount);
    }

    [Fact]
    public async Task GetById_WhenReaderFails_ReturnsSafeServerProblem()
    {
        using var factory = new StockFlowApiFactory();
        const string sensitiveDetail = "Private database connection information";

        factory.SupplierReader.ReadException =
            new InvalidOperationException(sensitiveDetail);

        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/suppliers/{Guid.NewGuid()}");

        var problem = await AssertProblem(
            response, HttpStatusCode.InternalServerError);

        Assert.DoesNotContain(sensitiveDetail, problem.GetRawText());
        Assert.False(string.IsNullOrWhiteSpace(
            problem.GetProperty("traceId").GetString()));
        Assert.Equal(1, factory.SupplierReader.ByIdCallCount);
    }

    [Fact]
    public async Task Create_ReturnsLocationPointingToDetailsRoute()
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/suppliers",
            new
            {
                code = "DEMO-LOCATION",
                name = "[DEMO] Location Test",
                contactEmail = "location@example.com"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content
            .ReadFromJsonAsync<CreateSupplierResult>();

        Assert.NotNull(created);
        Assert.NotNull(response.Headers.Location);

        var location = new Uri(client.BaseAddress!, response.Headers.Location);
        Assert.Equal($"/api/suppliers/{created.SupplierId}", location.AbsolutePath);

        // The write and read fakes are independent: configure the read result.
        var supplier = new SupplierDto(
            created.SupplierId, created.Code, created.Name,
            created.ContactEmail, created.PhoneNumber, created.IsActive);

        factory.SupplierReader.SupplierByIdResult = supplier;

        using var detailsResponse = await client.GetAsync(location);

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Equal(supplier,
            await detailsResponse.Content.ReadFromJsonAsync<SupplierDto>());
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