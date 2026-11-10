namespace StockFlow.API.Antiforgery
{
    public static class AntiforgeryRegistration
    {
        public const string HeaderName = "X-CSRF-TOKEN";

        public static IServiceCollection AddStockFlowAntiforgery(
            this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddAntiforgery(options =>
            {
                options.HeaderName = HeaderName;

                options.Cookie.Name = "__Host-StockFlow.Csrf";
                options.Cookie.Path = "/";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
            });

            return services;
        }
    }
}
