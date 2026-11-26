using Microsoft.Extensions.DependencyInjection;
using StockFlow.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Identity;
using StockFlow.Infrastructure.Persistence;
using StockFlow.Infrastructure.Persistence.Repositories;
using StockFlow.Application.StockReservations;
using StockFlow.Infrastructure.StockReservations;

namespace StockFlow.Infrastructure.Tests
{
    public class DependencyInjectionTests
    {
        [Fact]
        public void AddInfrastructure_RegistersRepositoriesWithScopedLifetimes()
        {
            // Arrange
            var services = new ServiceCollection();

            services.AddAuthentication().AddIdentityCookies();

            services.AddInfrastructure(
                "Host=localhost;Database=stockflow_di_test");

            using var provider = services.BuildServiceProvider(
                new ServiceProviderOptions
                {
                    ValidateScopes = true,
                    ValidateOnBuild = true
                });

            using var firstScope = provider.CreateScope();
            using var secondScope = provider.CreateScope();

            // Act
            var firstContext = firstScope.ServiceProvider
                .GetRequiredService<StockFlowDbContext>();

            var secondContext = secondScope.ServiceProvider
                .GetRequiredService<StockFlowDbContext>();

            var inventories = firstScope.ServiceProvider
                .GetRequiredService<IInventoryRepository>();

            var products = firstScope.ServiceProvider
                .GetRequiredService<IProductRepository>();

            var warehouses = firstScope.ServiceProvider
                .GetRequiredService<IWarehouseRepository>();

            var reservations = firstScope.ServiceProvider
                .GetRequiredService<IStockReservationRepository>();

            // Assert
            Assert.IsType<InventoryRepository>(inventories);
            Assert.IsType<ProductRepository>(products);
            Assert.IsType<WarehouseRepository>(warehouses);
            Assert.IsType<StockReservationRepository>(reservations);
            var reservationReader = firstScope.ServiceProvider.GetRequiredService<IStockReservationReadService>();
            Assert.IsType<StockReservationReadService>(reservationReader);
            Assert.Same(reservationReader,
                firstScope.ServiceProvider.GetRequiredService<IStockReservationReadService>());
            Assert.NotSame(reservationReader,
                secondScope.ServiceProvider.GetRequiredService<IStockReservationReadService>());
            Assert.Same(
                reservations,
                firstScope.ServiceProvider.GetRequiredService<IStockReservationRepository>());
            Assert.NotSame(
                reservations,
                secondScope.ServiceProvider.GetRequiredService<IStockReservationRepository>());

            Assert.Same(
                firstContext,
                firstScope.ServiceProvider
                    .GetRequiredService<StockFlowDbContext>());

            Assert.NotSame(firstContext, secondContext);

            Assert.Same(
                inventories,
                firstScope.ServiceProvider
                    .GetRequiredService<IInventoryRepository>());

            Assert.NotSame(
                inventories,
                secondScope.ServiceProvider
                    .GetRequiredService<IInventoryRepository>());

            Assert.Same(
                products,
                firstScope.ServiceProvider
                    .GetRequiredService<IProductRepository>());

            Assert.NotSame(
                products,
                secondScope.ServiceProvider
                    .GetRequiredService<IProductRepository>());

            Assert.Same(
                warehouses,
                firstScope.ServiceProvider
                    .GetRequiredService<IWarehouseRepository>());

            Assert.NotSame(
                warehouses,
                secondScope.ServiceProvider
                    .GetRequiredService<IWarehouseRepository>());
        }
    }
}
