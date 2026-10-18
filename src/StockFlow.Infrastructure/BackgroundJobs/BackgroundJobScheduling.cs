using Hangfire;

namespace StockFlow.Infrastructure.BackgroundJobs
{
    public static class BackgroundJobScheduling
    {
        public static void RegisterRecurringJobs(
            IRecurringJobManager recurringJobs)
        {
            ArgumentNullException.ThrowIfNull(recurringJobs);

            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(
                "Europe/Luxembourg");

            recurringJobs.AddOrUpdate<LowStockMonitoringJob>(
                "low-stock-monitoring",
                job => job.ExecuteAsync(CancellationToken.None),
                Cron.Daily(7, 0),
                new RecurringJobOptions
                {
                    TimeZone = timeZone
                });
        }
    }
}
