using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StockFlow.API.Authorization;
using StockFlow.Infrastructure.Identity;

namespace StockFlow.API.Tests;

public class AuthenticationConfigurationTests
{
    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStockFlowAuthentication();
        return services.BuildServiceProvider();
    }

    private static CookieAuthenticationOptions GetCookieOptions(
        IServiceProvider services)
    {
        return services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
    }

    [Fact]
    public void Authentication_UsesIdentityApplicationScheme()
    {
        using var services = CreateServices();

        var options = services
            .GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        Assert.Equal(
            IdentityConstants.ApplicationScheme,
            options.DefaultAuthenticateScheme);
        Assert.Equal(
            IdentityConstants.ApplicationScheme,
            options.DefaultChallengeScheme);
        Assert.Equal(
            IdentityConstants.ApplicationScheme,
            options.DefaultSignInScheme);
    }

    [Fact]
    public void Cookie_UsesHostPrefixAndHostOnlyScope()
    {
        using var services = CreateServices();
        var options = GetCookieOptions(services);

        Assert.Equal("__Host-StockFlow.Auth", options.Cookie.Name);
        Assert.Equal("/", options.Cookie.Path);
        Assert.Null(options.Cookie.Domain);
    }

    [Fact]
    public void Cookie_RequiresSecureHttpOnlyAndSameSiteLax()
    {
        using var services = CreateServices();
        var options = GetCookieOptions(services);

        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(
            CookieSecurePolicy.Always,
            options.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Lax, options.Cookie.SameSite);
    }

    [Fact]
    public void Cookie_ConfiguresFixedSixtyMinuteLifetime()
    {
        using var services = CreateServices();
        var options = GetCookieOptions(services);

        Assert.Equal(TimeSpan.FromMinutes(60), options.ExpireTimeSpan);
        Assert.False(options.SlidingExpiration);
    }

    [Fact]
    public void SecurityStamp_ConfiguresValidationWithoutIntervalDelay()
    {
        using var services = CreateServices();

        var options = services
            .GetRequiredService<IOptions<SecurityStampValidatorOptions>>()
            .Value;

        Assert.Equal(TimeSpan.Zero, options.ValidationInterval);
    }

    [Fact]
    public void Cookie_PreservesIdentitySecurityStampValidation()
    {
        using var services = CreateServices();
        var options = GetCookieOptions(services);

        Func<CookieValidatePrincipalContext, Task> expected =
            SecurityStampValidator.ValidatePrincipalAsync;

        Assert.Equal(expected, options.Events.OnValidatePrincipal);
    }

    [Theory]
    [InlineData(401, "Authentication required")]
    [InlineData(403, "Access denied")]
    public async Task RedirectEvents_ReturnProblemDetailsInsteadOfRedirecting(
        int expectedStatus,
        string expectedTitle)
    {
        using var services = CreateServices();
        var options = GetCookieOptions(services);
        using var body = new MemoryStream();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };

        httpContext.Request.Path = "/api/suppliers";
        httpContext.Response.Body = body;

        var scheme = new AuthenticationScheme(
            IdentityConstants.ApplicationScheme,
            displayName: null,
            handlerType: typeof(CookieAuthenticationHandler));

        var redirect = new RedirectContext<CookieAuthenticationOptions>(
            httpContext,
            scheme,
            options,
            new AuthenticationProperties(),
            "https://localhost/login");

        if (expectedStatus == 401)
            await options.Events.OnRedirectToLogin(redirect);
        else
            await options.Events.OnRedirectToAccessDenied(redirect);

        Assert.Equal(expectedStatus, httpContext.Response.StatusCode);
        Assert.StartsWith(
            "application/problem+json",
            httpContext.Response.ContentType!);
        Assert.False(httpContext.Response.Headers.ContainsKey("Location"));

        body.Position = 0;
        using var document = await JsonDocument.ParseAsync(body);
        var problem = document.RootElement;

        Assert.Equal(
            expectedStatus,
            problem.GetProperty("status").GetInt32());
        Assert.Equal(
            expectedTitle,
            problem.GetProperty("title").GetString());
        Assert.Equal(
            "/api/suppliers",
            problem.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            problem.GetProperty("traceId").GetString()));
    }

    [Fact]
    public void Api_ResolvesIdentityAccountServices()
    {
        using var factory = new StockFlowApiFactory();
        using var scope = factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>());
        Assert.NotNull(scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole<Guid>>>());
        Assert.NotNull(scope.ServiceProvider
            .GetRequiredService<SignInManager<ApplicationUser>>());
    }
}