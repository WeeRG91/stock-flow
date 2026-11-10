using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.Application.Abstractions.Authentication;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Abstractions.Time;
using StockFlow.Application.Inventory.GetInventory;
using StockFlow.Application.Inventory.MonitorLowStock;
using StockFlow.Application.Products.GetProducts;
using StockFlow.Application.Suppliers;
using StockFlow.Application.Warehouses.GetWarehouses;
using StockFlow.Infrastructure.Identity;
using StockFlow.Infrastructure.Inventory.GetInventory;
using StockFlow.Infrastructure.Inventory.MonitorLowStock;
using StockFlow.Infrastructure.Persistence;
using StockFlow.Infrastructure.Persistence.Repositories;
using StockFlow.Infrastructure.Products.GetProducts;
using StockFlow.Infrastructure.Suppliers;
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
            services.AddDbContext<StockFlowIdentityDbContext>(options =>
                options.UseNpgsql(
                    connectionString,
                    postgres => postgres.MigrationsHistoryTable(
                        "__EFMigrationsHistory",
                        "identity")));

            services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = 15;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<StockFlowIdentityDbContext>()
            .AddSignInManager();
            services.AddScoped<IAccountService, IdentityAccountService>();

            services.AddScoped<IInventoryRepository, InventoryRepository>();
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IWarehouseRepository, WarehouseRepository>();
            services.AddSingleton<IClock, SystemClock>();
            services.AddScoped<IProductReadService, ProductReadService>();
            services.AddScoped<IWarehouseReadService, WarehouseReadService>();
            services.AddScoped<IInventoryReadService, InventoryReadService>();
            services.AddScoped<IInventoryMonitoringReader, InventoryMonitoringReader>();
            services.AddScoped<ILowStockAlertStore, LowStockAlertStore>();
            services.AddScoped<ISupplierRepository, SupplierRepository>();
            services.AddScoped<ISupplierReadService, SupplierReadService>();

            return services;
        }
    }
}
