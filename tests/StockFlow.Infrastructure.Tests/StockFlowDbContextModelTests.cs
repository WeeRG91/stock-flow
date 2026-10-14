using Microsoft.EntityFrameworkCore;
using StockFlow.Domain.Entities;
using StockFlow.Infrastructure.Persistence;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Tests
{
    public class StockFlowDbContextModelTests
    {
        [Fact]
        public void Model_ContainsAllFourEntities()
        {
            // Arrange
            var options =
                new DbContextOptionsBuilder<StockFlowDbContext>()
                    .UseNpgsql(
                        "Host=localhost;Database=stockflow_model_test")
                    .Options;

            using var context = new StockFlowDbContext(options);

            // Act
            var model = context.Model;

            // Assert
            Assert.Equal(4, model.GetEntityTypes().Count());

            Assert.Equal(
                "Products",
                model.FindEntityType(typeof(Product))!.GetTableName());

            Assert.Equal(
                "Warehouses",
                model.FindEntityType(typeof(Warehouse))!.GetTableName());

            Assert.Equal(
                "Inventories",
                model.FindEntityType(typeof(InventoryEntity))!.GetTableName());

            Assert.Equal(
                "StockMovements",
                model.FindEntityType(typeof(StockMovement))!.GetTableName());
        }
    }
}