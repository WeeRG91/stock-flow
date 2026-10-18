using Microsoft.Extensions.Logging;

namespace StockFlow.Infrastructure.BackgroundJobs
{
    public sealed class DevelopmentDemoJob
    {
        private readonly ILogger<DevelopmentDemoJob> _logger;

        public DevelopmentDemoJob(ILogger<DevelopmentDemoJob> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }

        public Task ExecuteAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation(
               "StockFlow development demonstration job executed successfully.");

            return Task.CompletedTask;
        }
    }
}
