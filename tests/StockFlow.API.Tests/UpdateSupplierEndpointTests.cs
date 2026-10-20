using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using StockFlow.Application.Suppliers;
using StockFlow.Domain.Entities;

namespace StockFlow.API.Tests;

public class UpdateSupplierEndpointTests
{
    private static Supplier SeedSupplier(StockFlowApiFactory factory)
    {
        var supplier = new Supplier(
            "DEMO-OFFICE", "Original", "original@example.com", "123");

        factory.Suppliers.Add(supplier);
        return supplier;
    }

    private static JsonObject ValidRequest() => new()
    {
        ["name"] = " Updated Supplier ",
        ["contactEmail"] = " Sales@example.com ",
        ["phoneNumber"] = " 456 "
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Update_ReturnsNormalizedSupplierAndPreservesIdentity(
        bool isActive)
    {
        using var factory = new StockFlowApiFactory();
        var supplier = SeedSupplier(factory);

        if (!isActive)
            supplier.Deactivate();

        using var client = factory.CreateClient();
        using var response = await client.PutAsJsonAsync(
            $"/api/suppliers/{supplier.Id}", ValidRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<SupplierDto>();

        Assert.NotNull(result);
        Assert.Equal(supplier.Id, result.Id);
        Assert.Equal("DEMO-OFFICE", result.Code);
        Assert.Equal("Updated Supplier", result.Name);
        Assert.Equal("Sales@example.com", result.ContactEmail);
        Assert.Equal("456", result.PhoneNumber);
        Assert.Equal(isActive, result.IsActive);
        Assert.Same(supplier, factory.Suppliers.LastSavedSupplier);
        Assert.Equal(1, factory.Suppliers.SaveCallCount);
        Assert.Equal(0, factory.Suppliers.ExistsCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Update_WithBlankPhone_ClearsPhone(string? phone)
    {
        using var factory = new StockFlowApiFactory();
        var supplier = SeedSupplier(factory);
        var request = ValidRequest();
        request["phoneNumber"] = phone;

        using var client = factory.CreateClient();
        using var response = await client.PutAsJsonAsync(
            $"/api/suppliers/{supplier.Id}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SupplierDto>();

        Assert.NotNull(result);
        Assert.Null(result.PhoneNumber);
        Assert.Null(supplier.PhoneNumber);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("contactEmail")]
    public async Task Update_WithMissingRequiredField_ReturnsBadRequest(
        string field)
    {
        using var factory = new StockFlowApiFactory();
        var supplier = SeedSupplier(factory);
        var request = ValidRequest();
        request.Remove(field);

        using var client = factory.CreateClient();
        using var response = await client.PutAsJsonAsync(
            $"/api/suppliers/{supplier.Id}", request);

        await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Equal(0, factory.Suppliers.GetByIdCallCount);
        Assert.Equal(0, factory.Suppliers.SaveCallCount);
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("ContactEmail")]
    [InlineData("PhoneNumber")]
    public async Task Update_WithInvalidDetails_PreservesState(
        string field)
    {
        using var factory = new StockFlowApiFactory();
        var supplier = SeedSupplier(factory);
        var request = ValidRequest();

        if (field == "Name")
            request["name"] = new string('N', Supplier.MaxNameLength + 1);
        else if (field == "ContactEmail")
            request["contactEmail"] = "invalid";
        else
            request["phoneNumber"] =
                new string('1', Supplier.MaxPhoneNumberLength + 1);

        using var client = factory.CreateClient();
        using var response = await client.PutAsJsonAsync(
            $"/api/suppliers/{supplier.Id}", request);

        var problem = await AssertProblem(
            response, HttpStatusCode.BadRequest);

        Assert.True(problem.GetProperty("errors")
            .TryGetProperty(field, out _));
        Assert.Equal("Original", supplier.Name);
        Assert.Equal("original@example.com", supplier.ContactEmail);
        Assert.Equal("123", supplier.PhoneNumber);
        Assert.Equal(0, factory.Suppliers.SaveCallCount);
    }

    [Fact]
    public async Task Update_WhenMissing_ReturnsNotFound()
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            $"/api/suppliers/{Guid.NewGuid()}", ValidRequest());

        await AssertProblem(response, HttpStatusCode.NotFound);
        Assert.Equal(0, factory.Suppliers.SaveCallCount);
    }

    [Fact]
    public async Task Update_WithEmptyId_ReturnsBadRequest()
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            $"/api/suppliers/{Guid.Empty}", ValidRequest());

        var problem = await AssertProblem(
            response, HttpStatusCode.BadRequest);

        Assert.True(problem.GetProperty("errors")
            .TryGetProperty("SupplierId", out _));
        Assert.Equal(0, factory.Suppliers.GetByIdCallCount);
        Assert.Equal(0, factory.Suppliers.SaveCallCount);
    }

    [Fact]
    public async Task Update_WithMalformedId_DoesNotMatchRoute()
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/api/suppliers/not-a-guid", ValidRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, factory.Suppliers.GetByIdCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_WhenRepositoryFails_ReturnsSafeServerProblem(
        bool duringSave)
    {
        using var factory = new StockFlowApiFactory();
        var supplier = SeedSupplier(factory);
        const string sensitiveDetail = "Private persistence information";
        var failure = new InvalidOperationException(sensitiveDetail);

        if (duringSave)
            factory.Suppliers.SaveException = failure;
        else
            factory.Suppliers.LookupException = failure;

        using var client = factory.CreateClient();
        using var response = await client.PutAsJsonAsync(
            $"/api/suppliers/{supplier.Id}", ValidRequest());

        var problem = await AssertProblem(
            response, HttpStatusCode.InternalServerError);

        Assert.DoesNotContain(sensitiveDetail, problem.GetRawText());
        Assert.False(string.IsNullOrWhiteSpace(
            problem.GetProperty("traceId").GetString()));
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