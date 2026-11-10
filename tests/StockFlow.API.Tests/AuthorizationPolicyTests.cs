using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.API.Authorization;
using StockFlow.Application.Authorization;

namespace StockFlow.API.Tests;

public class AuthorizationPolicyTests
{
    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStockFlowAuthorization();
        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal CreateUser(
        bool authenticated,
        params string[] roles)
    {
        var claims = roles.Select(
            role => new Claim(ClaimTypes.Role, role));

        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            authenticated ? "PolicyTest" : null));
    }

    private static readonly string[] Policies =
    [
        StockFlowPolicies.CatalogRead,
        StockFlowPolicies.InventoryRead,
        StockFlowPolicies.InventoryReceive,
        StockFlowPolicies.SuppliersRead,
        StockFlowPolicies.SuppliersManage,
        StockFlowPolicies.BackgroundJobsManage
    ];

    public static IEnumerable<object[]> RoleMatrix()
    {
        var roles = new[]
        {
            (StockFlowRoles.Administrator, true, true, true),
            (StockFlowRoles.InventoryManager, true, false, false),
            (StockFlowRoles.PurchasingOfficer, false, true, false),
            (StockFlowRoles.PurchasingManager, false, true, false),
            (StockFlowRoles.WarehouseStaff, true, false, false),
            (StockFlowRoles.Viewer, false, false, false)
        };

        foreach (var (role, receive, manageSuppliers, manageJobs) in roles)
        {
            yield return [role, StockFlowPolicies.CatalogRead, true];
            yield return [role, StockFlowPolicies.InventoryRead, true];
            yield return [role, StockFlowPolicies.SuppliersRead, true];
            yield return [role, StockFlowPolicies.InventoryReceive, receive];
            yield return [role, StockFlowPolicies.SuppliersManage, manageSuppliers];
            yield return [role, StockFlowPolicies.BackgroundJobsManage, manageJobs];
        }
    }

    [Theory]
    [MemberData(nameof(RoleMatrix))]
    public async Task Policy_EnforcesRoleMatrix(
        string role,
        string policy,
        bool expected)
    {
        using var services = CreateServices();
        var authorization =
            services.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(
            CreateUser(true, role),
            resource: null,
            policyName: policy);

        Assert.Equal(expected, result.Succeeded);
    }

    public static IEnumerable<object[]> RejectedUsers()
    {
        foreach (var policy in Policies)
        {
            // Authenticated but without an assigned role.
            yield return [policy, true, ""];

            // Authenticated with an unrecognized role.
            yield return [policy, true, "UnknownRole"];

            // Role claims alone must not authenticate someone.
            yield return [policy, false, StockFlowRoles.Administrator];
        }
    }

    [Theory]
    [MemberData(nameof(RejectedUsers))]
    public async Task Policy_RejectsUsersWithoutRequiredAccess(
        string policy,
        bool authenticated,
        string role)
    {
        using var services = CreateServices();
        var authorization =
            services.GetRequiredService<IAuthorizationService>();

        var user = string.IsNullOrEmpty(role)
            ? CreateUser(authenticated)
            : CreateUser(authenticated, role);

        var result = await authorization.AuthorizeAsync(
            user,
            resource: null,
            policyName: policy);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FallbackPolicy_RequiresAuthentication(bool authenticated)
    {
        using var services = CreateServices();
        var provider =
            services.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorization =
            services.GetRequiredService<IAuthorizationService>();

        var fallback = await provider.GetFallbackPolicyAsync();

        Assert.NotNull(fallback);

        var result = await authorization.AuthorizeAsync(
            CreateUser(authenticated),
            resource: null,
            policy: fallback);

        Assert.Equal(authenticated, result.Succeeded);
    }

    [Fact]
    public async Task MultipleRoles_CombineTheirGrantedCapabilities()
    {
        using var services = CreateServices();
        var authorization =
            services.GetRequiredService<IAuthorizationService>();

        var user = CreateUser(
            true,
            StockFlowRoles.InventoryManager,
            StockFlowRoles.PurchasingOfficer);

        var receive = await authorization.AuthorizeAsync(
            user, null, StockFlowPolicies.InventoryReceive);
        var suppliers = await authorization.AuthorizeAsync(
            user, null, StockFlowPolicies.SuppliersManage);
        var jobs = await authorization.AuthorizeAsync(
            user, null, StockFlowPolicies.BackgroundJobsManage);

        Assert.True(receive.Succeeded);
        Assert.True(suppliers.Succeeded);
        Assert.False(jobs.Succeeded);
    }
}