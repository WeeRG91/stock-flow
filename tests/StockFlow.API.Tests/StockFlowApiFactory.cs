using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockFlow.API.Tests.Fakes;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Abstractions.Time;
using StockFlow.Domain.Entities;

namespace StockFlow.API.Tests
{
    public sealed class StockFlowApiFactory : WebApplicationFactory<Program>
    {
        public Product Product { get; } =
            new("Office Chair", "CHAIR-BLK");

        public Warehouse Warehouse { get; } =
            new("BRU-01", "Brussels Warehouse", "Brussels, Belgium");

        public TestProductRepository Products { get; } = new();
        public TestWarehouseRepository Warehouses { get; } = new();
        public TestInventoryRepository Inventories { get; } = new();

        public DateTimeOffset OccurredAt { get; } =
            new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

        public StockFlowApiFactory()
        {
            Products.Product = Product;
            Warehouses.Warehouse = Warehouse;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.UseSetting(
                "ConnectionStrings:StockFlow",
                "Host=localhost;Database=unused_api_test");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IProductRepository>();
                services.RemoveAll<IWarehouseRepository>();
                services.RemoveAll<IInventoryRepository>();
                services.RemoveAll<IClock>();

                services.AddSingleton<IProductRepository>(Products);
                services.AddSingleton<IWarehouseRepository>(Warehouses);
                services.AddSingleton<IInventoryRepository>(Inventories);
                services.AddSingleton<IClock>(
                    new FixedClock(OccurredAt));
            });
        }

        private sealed class FixedClock : IClock
        {
            public DateTimeOffset UtcNow { get; }

            public FixedClock(DateTimeOffset utcNow)
            {
                UtcNow = utcNow;
            }
        }
    }
}