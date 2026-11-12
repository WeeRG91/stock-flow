using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockFlow.API.Contracts.Authentication;
using StockFlow.API.Tests.Fakes;
using StockFlow.Application.Abstractions.Authentication;
using StockFlow.Application.Authentication;
using StockFlow.Application.Authorization;

namespace StockFlow.API.Tests;

public class CurrentUserEndpointTests
{
    private static readonly string[] ReadCapabilities =
    [
        StockFlowPolicies.CatalogRead,
        StockFlowPolicies.InventoryRead,
        StockFlowPolicies.SuppliersRead,
        StockFlowPolicies.PurchaseOrdersRead
    ];

    public static IEnumerable<object[]> CapabilityCases()
    {
        yield return
        [
            new[] { StockFlowRoles.Administrator },
            ReadCapabilities
                .Append(StockFlowPolicies.InventoryReceive)
                .Append(StockFlowPolicies.SuppliersManage)
                .Append(StockFlowPolicies.BackgroundJobsManage)
                .Append(StockFlowPolicies.PurchaseOrdersManage)
                .Append(StockFlowPolicies.PurchaseOrdersApprove)
                .ToArray()
        ];

        yield return
        [
            new[] { StockFlowRoles.InventoryManager },
            ReadCapabilities
                .Append(StockFlowPolicies.InventoryReceive)
                .ToArray()
        ];

        yield return
        [
            new[] { StockFlowRoles.WarehouseStaff },
            ReadCapabilities
                .Append(StockFlowPolicies.InventoryReceive)
                .ToArray()
        ];

        yield return
        [
            new[] { StockFlowRoles.PurchasingOfficer },
            ReadCapabilities
                .Append(StockFlowPolicies.SuppliersManage)
                .Append(StockFlowPolicies.PurchaseOrdersManage)
                .ToArray()
        ];

        yield return
        [
            new[] { StockFlowRoles.PurchasingManager },
            ReadCapabilities
                .Append(StockFlowPolicies.SuppliersManage)
                .Append(StockFlowPolicies.PurchaseOrdersManage)
                .Append(StockFlowPolicies.PurchaseOrdersApprove)
                .ToArray()
        ];

        yield return
        [
            new[] { StockFlowRoles.Viewer },
            ReadCapabilities
        ];

        yield return
        [
            new[] { "UnknownRole" },
            Array.Empty<string>()
        ];

        yield return
        [
            Array.Empty<string>(),
            Array.Empty<string>()
        ];
    }

    [Theory]
    [MemberData(nameof(CapabilityCases))]
    public async Task Me_ReturnsCapabilitiesGrantedByPolicies(
        string[] roles,
        string[] expectedCapabilities)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = roles;

        var profile = CreateProfile(factory.User);

        using var host = CreateHost(factory, profile);
        using var client = host.CreateClient();

        using var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body =
            await response.Content.ReadFromJsonAsync<CurrentUserResponse>();

        Assert.NotNull(body);
        Assert.Equal(profile.Id, body.Id);
        Assert.Equal(profile.Email, body.Email);
        Assert.Equal(profile.DisplayName, body.DisplayName);
        Assert.Equal(roles, body.Roles.ToArray());

        AssertCapabilities(expectedCapabilities, body.Capabilities);

        Assert.Equal(
            body.Capabilities.Count,
            body.Capabilities.Distinct().Count());
    }

    [Fact]
    public async Task Me_WithMultipleRoles_CombinesCapabilities()
    {
        using var factory = new StockFlowApiFactory();

        factory.User.Roles =
        [
            StockFlowRoles.WarehouseStaff,
            StockFlowRoles.PurchasingOfficer
        ];

        using var host = CreateHost(
            factory,
            CreateProfile(factory.User));

        using var client = host.CreateClient();
        using var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body =
            await response.Content.ReadFromJsonAsync<CurrentUserResponse>();

        Assert.NotNull(body);

        AssertCapabilities(
            ReadCapabilities
                .Append(StockFlowPolicies.InventoryReceive)
                .Append(StockFlowPolicies.SuppliersManage)
                .Append(StockFlowPolicies.PurchaseOrdersManage),
            body.Capabilities);
    }

    [Fact]
    public async Task Me_UsesAuthenticatedPrincipalForCapabilities()
    {
        using var factory = new StockFlowApiFactory();

        factory.User.Roles = [StockFlowRoles.Viewer];

        // Deliberately different from the authenticated principal.
        var profile = CreateProfile(factory.User) with
        {
            Roles = new[] { StockFlowRoles.Administrator }
        };

        using var host = CreateHost(factory, profile);
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body =
            await response.Content.ReadFromJsonAsync<CurrentUserResponse>();

        Assert.NotNull(body);
        AssertCapabilities(ReadCapabilities, body.Capabilities);
    }

    [Fact]
    public async Task Me_WhenAnonymous_ReturnsUnauthorized()
    {
        using var factory = new StockFlowApiFactory();
        factory.User.IsAuthenticated = false;

        using var host = CreateHost(
            factory,
            CreateProfile(factory.User));

        using var client = host.CreateClient();
        using var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WhenProfileIsMissing_ReturnsUnauthorized()
    {
        using var factory = new StockFlowApiFactory();
        using var host = CreateHost(factory, profile: null);
        using var client = host.CreateClient();

        using var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static UserProfileDto CreateProfile(TestUserIdentity user)
    {
        return new UserProfileDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Roles);
    }

    private static WebApplicationFactory<Program> CreateHost(
        StockFlowApiFactory factory,
        UserProfileDto? profile)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAccountService>();

                services.AddSingleton<IAccountService>(
                    new TestAccountService
                    {
                        Profile = profile
                    });
            });
        });
    }

    private static void AssertCapabilities(
        IEnumerable<string> expected,
        IEnumerable<string> actual)
    {
        Assert.Equal(
            expected.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            actual.OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }
}
