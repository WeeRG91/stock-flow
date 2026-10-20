using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StockFlow.Application.Exceptions;
using StockFlow.Application.Suppliers.CreateSupplier;
using StockFlow.Domain.Entities;

namespace StockFlow.API.Tests;

public class CreateSupplierEndpointTests
{
    private const string Endpoint = "/api/suppliers";

    private static JsonObject ValidRequest() => new()
    {
        ["code"] = "  sup-001  ",
        ["name"] = "  Office Supplier  ",
        ["contactEmail"] = "  Sales@Example.com  ",
        ["phoneNumber"] = "  +352 020 30 40  "
    };

    private static async Task<JsonElement> AssertProblem(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            (int)expectedStatus,
            problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(
            problem.GetProperty("title").GetString()));

        return problem;
    }

    [Fact]
    public async Task Create_WithValidRequest_ReturnsNormalizedCreatedSupplier()
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            Endpoint, ValidRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<CreateSupplierResult>();

        Assert.NotNull(result);
        var saved = Assert.IsType<Supplier>(
            factory.Suppliers.LastSavedSupplier);

        Assert.Equal(saved.Id, result.SupplierId);
        Assert.NotEqual(Guid.Empty, result.SupplierId);
        Assert.Equal("SUP-001", result.Code);
        Assert.Equal("Office Supplier", result.Name);
        Assert.Equal("Sales@Example.com", result.ContactEmail);
        Assert.Equal("+352 020 30 40", result.PhoneNumber);
        Assert.True(result.IsActive);
        Assert.Equal("SUP-001", factory.Suppliers.LastCheckedCode);
        Assert.Equal(1, factory.Suppliers.SaveCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_WithMissingOrNullPhone_ReturnsCreated(
        bool explicitNull)
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();
        var request = ValidRequest();

        if (explicitNull)
            request["phoneNumber"] = null;
        else
            request.Remove("phoneNumber");

        using var response = await client.PostAsJsonAsync(Endpoint, request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<CreateSupplierResult>();

        Assert.NotNull(result);
        Assert.Null(result.PhoneNumber);
    }

    [Theory]
    [InlineData("code", "Code", "missing")]
    [InlineData("code", "Code", "null")]
    [InlineData("code", "Code", "empty")]
    [InlineData("code", "Code", "whitespace")]
    [InlineData("name", "Name", "missing")]
    [InlineData("name", "Name", "null")]
    [InlineData("name", "Name", "empty")]
    [InlineData("name", "Name", "whitespace")]
    [InlineData("contactEmail", "ContactEmail", "missing")]
    [InlineData("contactEmail", "ContactEmail", "null")]
    [InlineData("contactEmail", "ContactEmail", "empty")]
    [InlineData("contactEmail", "ContactEmail", "whitespace")]
    public async Task Create_WithMissingRequiredValue_ReturnsFieldValidation(
        string jsonField,
        string errorField,
        string kind)
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();
        var request = ValidRequest();

        if (kind == "missing")
            request.Remove(jsonField);
        else
            request[jsonField] = kind == "null"
                ? null
                : kind == "empty" ? "" : "   ";

        using var response = await client.PostAsJsonAsync(Endpoint, request);
        var problem = await AssertProblem(response, HttpStatusCode.BadRequest);

        Assert.True(problem.GetProperty("errors")
            .TryGetProperty(errorField, out _));
        Assert.Equal(0, factory.Suppliers.ExistsCallCount);
        Assert.Equal(0, factory.Suppliers.SaveCallCount);
    }

    [Fact]
    public async Task Create_WithMalformedEmail_ReturnsFieldValidation()
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();
        var request = ValidRequest();
        request["contactEmail"] = "not-an-email";

        using var response = await client.PostAsJsonAsync(Endpoint, request);
        var problem = await AssertProblem(response, HttpStatusCode.BadRequest);

        Assert.True(problem.GetProperty("errors")
            .TryGetProperty("ContactEmail", out _));
        Assert.Equal(0, factory.Suppliers.ExistsCallCount);
        Assert.Equal(0, factory.Suppliers.SaveCallCount);
    }

    [Theory]
    [InlineData("code", "Code")]
    [InlineData("name", "Name")]
    [InlineData("contactEmail", "ContactEmail")]
    [InlineData("phoneNumber", "PhoneNumber")]
    public async Task Create_WithOversizedField_ReturnsFieldValidation(
        string jsonField,
        string errorField)
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();
        var request = ValidRequest();

        request[jsonField] = jsonField switch
        {
            "code" => new string('A', Supplier.MaxCodeLength + 1),
            "name" => new string('N', Supplier.MaxNameLength + 1),
            "phoneNumber" =>
                new string('1', Supplier.MaxPhoneNumberLength + 1),
            _ => new string('a', 64) + "@" +
                new string('b', 63) + "." +
                new string('c', 63) + "." +
                new string('d', 62)
        };

        using var response = await client.PostAsJsonAsync(Endpoint, request);
        var problem = await AssertProblem(response, HttpStatusCode.BadRequest);

        Assert.True(problem.GetProperty("errors")
            .TryGetProperty(errorField, out _));
        Assert.Equal(0, factory.Suppliers.ExistsCallCount);
        Assert.Equal(0, factory.Suppliers.SaveCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_WithExistingCode_ReturnsConflict(bool inactive)
    {
        using var factory = new StockFlowApiFactory();
        var existing = new Supplier(
            "SUP-001", "Existing", "existing@example.com");

        if (inactive) existing.Deactivate();
        factory.Suppliers.Add(existing);

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            Endpoint, ValidRequest());

        var problem = await AssertProblem(response, HttpStatusCode.Conflict);

        Assert.Equal(
            "Supplier code conflict",
            problem.GetProperty("title").GetString());
        Assert.Contains(
            "SUP-001",
            problem.GetProperty("detail").GetString()!);
        Assert.Equal(0, factory.Suppliers.SaveCallCount);
    }

    [Fact]
    public async Task Create_WhenSaveReportsCodeConflict_ReturnsConflict()
    {
        using var factory = new StockFlowApiFactory();
        factory.Suppliers.SaveException =
            new SupplierCodeConflictException("SUP-001");

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            Endpoint, ValidRequest());

        await AssertProblem(response, HttpStatusCode.Conflict);

        Assert.Equal(1, factory.Suppliers.ExistsCallCount);
        Assert.Equal(1, factory.Suppliers.SaveCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_WhenRepositoryFails_ReturnsSafeServerError(
        bool duringSave)
    {
        using var factory = new StockFlowApiFactory();
        const string internalMessage = "Sensitive database diagnostics.";
        var failure = new InvalidOperationException(internalMessage);

        if (duringSave)
            factory.Suppliers.SaveException = failure;
        else
            factory.Suppliers.LookupException = failure;

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            Endpoint, ValidRequest());

        var problem = await AssertProblem(
            response, HttpStatusCode.InternalServerError);

        Assert.Equal(
            "An unexpected error occurred.",
            problem.GetProperty("detail").GetString());
        Assert.True(problem.TryGetProperty("traceId", out _));

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(internalMessage, body);
        Assert.DoesNotContain("InvalidOperationException", body);
    }

    [Fact]
    public async Task Create_WithMalformedJson_ReturnsBadRequest()
    {
        using var factory = new StockFlowApiFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(
            "{", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(Endpoint, content);

        await AssertProblem(response, HttpStatusCode.BadRequest);

        Assert.Equal(0, factory.Suppliers.ExistsCallCount);
        Assert.Equal(0, factory.Suppliers.SaveCallCount);
    }
}