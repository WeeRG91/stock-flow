using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.API.Authorization;
using StockFlow.Application.Abstractions;
using StockFlow.Application.Authorization;

namespace StockFlow.API.Tests;

public class HttpCurrentUserTests
{
    private static readonly Guid UserId =
        Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStockFlowAuthorization();

        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal CreatePrincipal(
        bool authenticated,
        string? identifier,
        params string[] roles)
    {
        var claims = new List<Claim>();

        if (identifier is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, identifier));
        }

        claims.AddRange(
            roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            authenticated ? "CurrentUserTest" : null));
    }

    private static HttpCurrentUser CreateAdapter(
        ServiceProvider services,
        ClaimsPrincipal? principal)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = principal is null
                ? null
                : new DefaultHttpContext { User = principal }
        };

        return new HttpCurrentUser(
            accessor,
            services.GetRequiredService<IAuthorizationService>());
    }

    [Fact]
    public void UserId_WithAuthenticatedValidIdentifier_ReturnsIdentifier()
    {
        using var services = CreateServices();

        var currentUser = CreateAdapter(
            services,
            CreatePrincipal(true, UserId.ToString()));

        Assert.Equal(UserId, currentUser.UserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void UserId_WithInvalidIdentifier_ReturnsNull(string? identifier)
    {
        using var services = CreateServices();

        var currentUser = CreateAdapter(
            services,
            CreatePrincipal(true, identifier));

        Assert.Null(currentUser.UserId);
    }

    [Fact]
    public async Task AnonymousPrincipal_CannotActEvenWithIdentityClaims()
    {
        using var services = CreateServices();

        var currentUser = CreateAdapter(
            services,
            CreatePrincipal(
                false,
                UserId.ToString(),
                StockFlowRoles.Administrator));

        Assert.Null(currentUser.UserId);
        Assert.False(await currentUser.HasPermissionAsync(
            StockFlowPolicies.BackgroundJobsManage));
    }

    [Fact]
    public async Task MissingHttpContext_ReturnsNoIdentityOrPermission()
    {
        using var services = CreateServices();
        var currentUser = CreateAdapter(services, principal: null);

        Assert.Null(currentUser.UserId);
        Assert.False(await currentUser.HasPermissionAsync(
            StockFlowPolicies.InventoryRead));
    }

    [Theory]
    [InlineData(
        StockFlowRoles.Viewer,
        StockFlowPolicies.InventoryRead,
        true)]
    [InlineData(
        StockFlowRoles.Viewer,
        StockFlowPolicies.InventoryReceive,
        false)]
    [InlineData(
        StockFlowRoles.WarehouseStaff,
        StockFlowPolicies.InventoryReceive,
        true)]
    [InlineData(
        StockFlowRoles.PurchasingOfficer,
        StockFlowPolicies.SuppliersManage,
        true)]
    [InlineData(
        StockFlowRoles.WarehouseStaff,
        StockFlowPolicies.SuppliersManage,
        false)]
    [InlineData(
        StockFlowRoles.Administrator,
        StockFlowPolicies.BackgroundJobsManage,
        true)]
    public async Task HasPermissionAsync_UsesRegisteredPolicies(
        string role,
        string permission,
        bool expected)
    {
        using var services = CreateServices();

        var currentUser = CreateAdapter(
            services,
            CreatePrincipal(true, UserId.ToString(), role));

        var actual = await currentUser.HasPermissionAsync(permission);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task HasPermissionAsync_WithInvalidActor_ReturnsFalse(
        string? identifier)
    {
        using var services = CreateServices();

        var currentUser = CreateAdapter(
            services,
            CreatePrincipal(
                true,
                identifier,
                StockFlowRoles.Administrator));

        Assert.False(await currentUser.HasPermissionAsync(
            StockFlowPolicies.BackgroundJobsManage));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HasPermissionAsync_WithMissingPermission_Throws(
        string? permission)
    {
        using var services = CreateServices();

        var currentUser = CreateAdapter(
            services,
            CreatePrincipal(true, UserId.ToString()));

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => currentUser.HasPermissionAsync(permission!));
    }

    [Fact]
    public async Task HasPermissionAsync_WithCanceledToken_Throws()
    {
        using var services = CreateServices();

        var currentUser = CreateAdapter(
            services,
            CreatePrincipal(
                true,
                UserId.ToString(),
                StockFlowRoles.Administrator));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => currentUser.HasPermissionAsync(
                StockFlowPolicies.InventoryRead,
                cancellation.Token));
    }

    [Fact]
    public void ApiRegistration_ResolvesCurrentUserAdapter()
    {
        using var factory = new StockFlowApiFactory();
        using var scope = factory.Services.CreateScope();

        var currentUser =
            scope.ServiceProvider.GetRequiredService<ICurrentUser>();

        Assert.IsType<HttpCurrentUser>(currentUser);
    }
}