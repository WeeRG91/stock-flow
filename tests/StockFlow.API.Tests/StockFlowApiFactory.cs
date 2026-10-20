using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using StockFlow.API.Hubs;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockFlow.API.Tests.Fakes;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Abstractions.Time;
using StockFlow.Application.Inventory.GetInventory;
using StockFlow.Application.Products.GetProducts;
using StockFlow.Application.Suppliers;
using StockFlow.Application.Warehouses.GetWarehouses;
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
        public TestSupplierRepository Suppliers { get; } = new();
        public TestProductReadService ProductReader { get; } = new();
        public TestWarehouseReadService WarehouseReader { get; } = new();
        public TestInventoryReadService InventoryReader { get; } = new();
        public TestSupplierReadService SupplierReader { get; } = new();
        public TestInventoryClientProxy NotificationClient { get; } = new();

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

            builder.UseSetting("BackgroundJobs:Enabled", "false");

            builder.UseSetting(
                "ConnectionStrings:StockFlow",
                "Host=localhost;Database=unused_api_test");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IProductRepository>();
                services.RemoveAll<IWarehouseRepository>();
                services.RemoveAll<IInventoryRepository>();
                services.RemoveAll<ISupplierRepository>();
                services.RemoveAll<IClock>();
                services.RemoveAll<IProductReadService>();
                services.RemoveAll<IWarehouseReadService>();
                services.RemoveAll<IInventoryReadService>();
                services.RemoveAll<ISupplierReadService>();
                services.RemoveAll<IHubContext<InventoryHub>>();

                services.AddSingleton<IProductRepository>(Products);
                services.AddSingleton<IWarehouseRepository>(Warehouses);
                services.AddSingleton<IInventoryRepository>(Inventories);
                services.AddSingleton<ISupplierRepository>(Suppliers);
                services.AddSingleton<IClock>(
                    new FixedClock(OccurredAt));
                services.AddSingleton<IProductReadService>(ProductReader);
                services.AddSingleton<IWarehouseReadService>(WarehouseReader);
                services.AddSingleton<IInventoryReadService>(InventoryReader);
                services.AddSingleton<ISupplierReadService>(SupplierReader);
                services.AddSingleton<IHubContext<InventoryHub>>(
                    new TestInventoryHubContext(NotificationClient));
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
