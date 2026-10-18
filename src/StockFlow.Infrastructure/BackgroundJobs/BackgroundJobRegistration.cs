using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.DependencyInjection;

namespace StockFlow.Infrastructure.BackgroundJobs
{
    public static class BackgroundJobRegistration
    {
        public static IServiceCollection AddStockFlowBackgroundJobs(
            this IServiceCollection services,
            string connectionString)
        {
            ArgumentNullException.ThrowIfNull(services);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException(
                    "Connection string must not be empty or whitespace.",
                    nameof(connectionString));
            }

            services.AddHangfire(configuration => configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(
                    options => options.UseNpgsqlConnection(connectionString),
                    new PostgreSqlStorageOptions
                    {
                        SchemaName = "hangfire",
                        PrepareSchemaIfNecessary = true,
                    }));
            services.AddTransient<DevelopmentDemoJob>();
            services.AddTransient<LowStockMonitoringJob>();

            return services;
        }
    }
}
