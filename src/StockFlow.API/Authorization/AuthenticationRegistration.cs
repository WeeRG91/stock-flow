using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace StockFlow.API.Authorization
{
    public static class AuthenticationRegistration
    {
        public static IServiceCollection AddStockFlowAuthentication(
            this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;

                options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;

                options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
            })
            .AddIdentityCookies();

            services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.Name = "__Host-StockFlow.Auth";
                options.Cookie.Path = "/";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;

                options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
                options.SlidingExpiration = false;

                options.Events.OnRedirectToLogin = context =>
                    WriteProblemAsync(
                        context.HttpContext,
                        StatusCodes.Status401Unauthorized,
                        "Authentication required");

                options.Events.OnRedirectToAccessDenied = context =>
                    WriteProblemAsync(
                        context.HttpContext,
                        StatusCodes.Status403Forbidden,
                        "Access denied");
            });

            services.Configure<SecurityStampValidatorOptions>(options =>
            {
                options.ValidationInterval = TimeSpan.Zero;
            });

            return services;
        }

        private static Task WriteProblemAsync(
            HttpContext context,
            int status,
            string title)
        {
            context.Response.StatusCode = status;

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Instance = context.Request.Path.Value
            };

            problem.Extensions["traceId"] = context.TraceIdentifier;

            return context.Response.WriteAsJsonAsync(
                problem,
                options: null,
                contentType: "application/problem+json",
                cancellationToken: context.RequestAborted);
        }
    }
}
