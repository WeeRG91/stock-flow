using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Threading.RateLimiting;

namespace StockFlow.API.RateLimiting
{
    public static class RateLimitingRegistration
    {
        public const string LoginPolicy = "Auth.Login";

        public static IServiceCollection AddStockFlowRateLimiting(
            this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.AddPolicy(LoginPolicy, context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey:
                            context.Connection.RemoteIpAddress?.ToString()
                            ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));

                options.OnRejected = async (context, cancellationToken) =>
                {
                    var httpContext = context.HttpContext;
                    httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                    if (context.Lease.TryGetMetadata(
                        MetadataName.RetryAfter,
                        out var retryAfter))
                    {
                        httpContext.Response.Headers["Retry-After"] =
                            Math.Ceiling(retryAfter.TotalSeconds)
                                .ToString(CultureInfo.InvariantCulture);
                    }

                    var problem = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests",
                        Detail = "Too many login attempts. Please try again later.",
                        Instance = httpContext.Request.Path.Value
                    };

                    problem.Extensions["traceId"] = httpContext.TraceIdentifier;

                    await httpContext.Response.WriteAsJsonAsync(
                        problem,
                        options: null,
                        contentType: "application/problem+json",
                        cancellationToken: cancellationToken);
                };
            });

            return services;
        }
    }
}
