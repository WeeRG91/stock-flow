using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StockFlow.Application.Suppliers;
using StockFlow.Application.Suppliers.GetSuppliers;

namespace StockFlow.API.Tests;

public class GetSuppliersEndpointTests
{
    private const string Endpoint = "/api/suppliers";

    [Fact]
    public async Task Get_WithDefaults_ReturnsEmptyPage()
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Endpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<GetSuppliersResult>();
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(1, factory.SupplierReader.CallCount);
        Assert.Equal(1, factory.SupplierReader.PageNumber);
        Assert.Equal(20, factory.SupplierReader.PageSize);
        Assert.Null(factory.SupplierReader.Search);
        Assert.Null(factory.SupplierReader.IsActive);
        Assert.Equal(0, factory.Suppliers.SaveCallCount);
    }

    [Fact]
    public async Task Get_ReturnsSupplierFieldsAndPaginationMetadata()
    {
        using var factory = new StockFlowApiFactory();
       
        var suppliers = new[]
        {
            new SupplierDto(Guid.NewGuid(), "DEMO-OFFICE", "[DEMO] Office Supplies",
                "office@example.com", "+352 000 001", true),
            new SupplierDto(Guid.NewGuid(), "DEMO-TECH", "[DEMO] Technology Supplies",
                "tech@example.com", null, false)
        };
        factory.SupplierReader.Result = new(suppliers, 2, 2, 5);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Endpoint + "?pageNumber=2&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<GetSuppliersResult>();
        Assert.NotNull(result);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(suppliers, result.Items.ToArray());
        Assert.Equal(2, factory.SupplierReader.PageNumber);
        Assert.Equal(2, factory.SupplierReader.PageSize);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("?isActive=true", true)]
    [InlineData("?isActive=false", false)]
    public async Task Get_ForwardsStatusFilter(string query, bool? expected)
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Endpoint + query);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, factory.SupplierReader.IsActive);
        Assert.Equal(1, factory.SupplierReader.CallCount);
    }

    [Theory]
    [InlineData("  office  ", "office")]
    [InlineData("   ", null)]
    [InlineData("", null)]
    [InlineData(" %_\\ ", "%_\\")]
    public async Task Get_ForwardsNormalizedSearch(string search, string? expected)
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            Endpoint + "?search=" + Uri.EscapeDataString(search));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, factory.SupplierReader.Search);
        Assert.Equal(1, factory.SupplierReader.CallCount);
    }

    [Theory]
    [InlineData("pageNumber=0", "PageNumber")]
    [InlineData("pageNumber=-1", "PageNumber")]
    [InlineData("pageSize=0", "PageSize")]
    [InlineData("pageSize=-1", "PageSize")]
    [InlineData("pageSize=101", "PageSize")]
    [InlineData("pageNumber=2147483647&pageSize=100", "PageNumber")]
    [InlineData("pageNumber=abc", "PageNumber")]
    [InlineData("pageSize=abc", "PageSize")]
    [InlineData("pageNumber=2147483648", "PageNumber")]
    [InlineData("isActive=perhaps", "IsActive")]
    public async Task Get_WithInvalidQuery_ReturnsValidationProblem(
        string query, string field)
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Endpoint + "?" + query);

        var problem = await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Contains(problem.GetProperty("errors").EnumerateObject(),
            error => string.Equals(error.Name, field, StringComparison.OrdinalIgnoreCase)
                && error.Value.GetArrayLength() > 0);
        Assert.Equal(0, factory.SupplierReader.CallCount);
    }

    [Fact]
    public async Task Get_WhenReaderFails_ReturnsSafeServerProblem()
    {
        using var factory = new StockFlowApiFactory();
        const string sensitiveDetail = "Private database connection information";
        factory.SupplierReader.ReadException = new InvalidOperationException(sensitiveDetail);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Endpoint);

        var problem = await AssertProblem(response, HttpStatusCode.InternalServerError);
        Assert.DoesNotContain(sensitiveDetail, problem.GetRawText());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        Assert.Equal(1, factory.SupplierReader.CallCount);
    }

    private static async Task<JsonElement> AssertProblem(
        HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        return problem;
    }
}
