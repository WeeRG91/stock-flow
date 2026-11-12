using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StockFlow.API.Errors;
using StockFlow.Application.Exceptions;

namespace StockFlow.API.Tests;

public class ApiAuthorizationExceptionTests
{
    [Theory]
    [InlineData(true, 401, "Authentication required")]
    [InlineData(false, 403, "Access denied")]
    public async Task ApplicationAuthorizationFailure_ReturnsProblemDetails(
        bool authenticationRequired,
        int expectedStatus,
        string expectedTitle)
    {
        var services = new ServiceCollection();
        services.AddOptions();

        using var provider = services.BuildServiceProvider();
        using var body = new MemoryStream();

        var context = new DefaultHttpContext
        {
            RequestServices = provider,
            TraceIdentifier = "authorization-test-trace"
        };

        context.Request.Path = "/api/purchase-orders";
        context.Response.Body = body;

        Exception exception = authenticationRequired
            ? new AuthenticationRequiredException()
            : new PermissionDeniedException();

        var handler = new ApiExceptionHandler(
            NullLogger<ApiExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            context,
            exception,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.NotNull(context.Response.ContentType);
        Assert.StartsWith(
            "application/problem+json",
            context.Response.ContentType);

        body.Position = 0;
        using var json = await JsonDocument.ParseAsync(body);
        var problem = json.RootElement;

        Assert.Equal(
            expectedStatus,
            problem.GetProperty("status").GetInt32());

        Assert.Equal(
            expectedTitle,
            problem.GetProperty("title").GetString());

        Assert.Equal(
            exception.Message,
            problem.GetProperty("detail").GetString());

        Assert.Equal(
            "/api/purchase-orders",
            problem.GetProperty("instance").GetString());

        Assert.Equal(
            "authorization-test-trace",
            problem.GetProperty("traceId").GetString());
    }
}