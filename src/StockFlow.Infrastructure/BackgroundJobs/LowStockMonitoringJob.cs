using Hangfire;
using Microsoft.Extensions.Logging;
using StockFlow.Application.Inventory.MonitorLowStock;

namespace StockFlow.Infrastructure.BackgroundJobs
{
    public sealed class LowStockMonitoringJob
    {
        private readonly MonitorLowStockCommandHandler _handler;
        private readonly ILogger<LowStockMonitoringJob> _logger;

        public LowStockMonitoringJob(
            MonitorLowStockCommandHandler handler,
            ILogger<LowStockMonitoringJob> logger)
        {
            ArgumentNullException.ThrowIfNull(handler);
            ArgumentNullException.ThrowIfNull(logger);

            _handler = handler;
            _logger = logger;
        }

        [AutomaticRetry(
            Attempts = 3,
            DelaysInSeconds = new[] { 30, 120, 300 },
            OnAttemptsExceeded = AttemptsExceededAction.Fail)]
        public async Task ExecuteAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Low-stock monitoring started.");

            var result = await _handler.HandleAsync(
                new MonitorLowStockCommand(),
                cancellationToken);

            _logger.LogInformation(
                "Low-stock monitoring completed. Created: {Created}; " +
                "Reopened: {Reopened}; Resolved: {Resolved}; " +
                "Active alerts: {ActiveCount}.",
                result.Created,
                result.Reopened,
                result.Resolved,
                result.ActiveCount);
        }
    }
}