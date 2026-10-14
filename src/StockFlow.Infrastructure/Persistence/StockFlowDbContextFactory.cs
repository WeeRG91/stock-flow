using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StockFlow.Infrastructure.Persistence
{
    public sealed class StockFlowDbContextFactory : IDesignTimeDbContextFactory<StockFlowDbContext>
    {
        public StockFlowDbContext CreateDbContext(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable(
                "STOCKFLOW_DEV_CONNECTION_STRING");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Set STOCKFLOW_DEV_CONNECTION_STRING before running EF tools.");
            }

            var options = new DbContextOptionsBuilder<StockFlowDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            return new StockFlowDbContext(options);
        }
    }
}
