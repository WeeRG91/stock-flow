using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StockFlow.Infrastructure.Identity
{
    public sealed class StockFlowIdentityDbContextFactory
        : IDesignTimeDbContextFactory<StockFlowIdentityDbContext>
    {
        public StockFlowIdentityDbContext CreateDbContext(string[] args)
        {
            var connectingString = Environment.GetEnvironmentVariable(
                "STOCKFLOW_DEV_CONNECTION_STRING");

            if (string.IsNullOrWhiteSpace(connectingString))
            {
                throw new InvalidOperationException(
                    "Set STOCKFLOW_DEV_CONNECTION_STRING before running EF tools.");
            }

            var options = new DbContextOptionsBuilder<StockFlowIdentityDbContext>()
                .UseNpgsql(
                    connectingString,
                    postgres => postgres.MigrationsHistoryTable(
                        "__EFMigrationsHistory",
                        "identity"))
                .Options;

            return new StockFlowIdentityDbContext(options);
        }
    }
}
