using Hangfire;
using Microsoft.Extensions.Logging;
using StockFlow.Application.PurchaseOrders.MonitorOverduePurchaseOrders;

namespace StockFlow.Infrastructure.BackgroundJobs
{
    public sealed class OverduePurchaseOrderMonitoringJob(
        MonitorOverduePurchaseOrdersCommandHandler handler,
        ILogger<OverduePurchaseOrderMonitoringJob> logger)
    {
        [AutomaticRetry(
            Attempts = 3,
            DelaysInSeconds = new[] { 30, 120, 300 },
            OnAttemptsExceeded = AttemptsExceededAction.Fail)]
        public async Task ExecuteAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            logger.LogInformation("Overdue purchase order monitoring started.");

            var result = await handler.HandleAsync(cancellationToken);

            logger.LogInformation(
                "Overdue purchase order monitoring completed. Created: {Created}; " +
                "Resolved: {Resolved}; Active alerts: {ActiveCount}.",
                result.Created,
                result.Resolved,
                result.ActiveCount);
        }
    }
}
