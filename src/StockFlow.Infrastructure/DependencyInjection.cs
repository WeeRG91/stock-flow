using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Abstractions.Time;
using StockFlow.Application.Inventory.GetInventory;
using StockFlow.Application.Products.GetProducts;
using StockFlow.Application.Warehouses.GetWarehouses;
using StockFlow.Infrastructure.Inventory.GetInventory;
using StockFlow.Infrastructure.Persistence;
using StockFlow.Infrastructure.Persistence.Repositories;
using StockFlow.Infrastructure.Products.GetProducts;
using StockFlow.Infrastructure.Time;
using StockFlow.Infrastructure.Warehouses.GetWarehouses;

namespace StockFlow.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
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

            services.AddDbContext<StockFlowDbContext>(options =>
                options.UseNpgsql(connectionString));

            services.AddScoped<IInventoryRepository, InventoryRepository>();
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IWarehouseRepository, WarehouseRepository>();
            services.AddSingleton<IClock, SystemClock>();
            services.AddScoped<IProductReadService, ProductReadService>();
            services.AddScoped<IWarehouseReadService, WarehouseReadService>();
            services.AddScoped<IInventoryReadService, InventoryReadService>();

            return services;
        }
    }
}
