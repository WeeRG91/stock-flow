using Microsoft.EntityFrameworkCore;
using StockFlow.Domain.Entities;
using StockFlow.Infrastructure.Persistence;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Tests
{
    public class StockFlowDbContextModelTests
    {
        [Fact]
        public void Model_ContainsAllSixEntities()
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
            Assert.Equal(6, model.GetEntityTypes().Count());

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

            Assert.Equal(
                "LowStockAlerts",
                model.FindEntityType(typeof(LowStockAlert))!.GetTableName());

            Assert.Equal(
                "Suppliers",
                model.FindEntityType(typeof(Supplier))!.GetTableName());
        }

        [Fact]
        public void Model_ConfiguresSupplierFieldsAndGlobalCodeUniqueness()
        {
            var options = new DbContextOptionsBuilder<StockFlowDbContext>()
                .UseNpgsql("Host=localhost;Database=stockflow_model_test")
                .Options;

            using var context = new StockFlowDbContext(options);
            var supplier = context.Model.FindEntityType(typeof(Supplier));
            Assert.NotNull(supplier);

            Assert.Equal(
                new[] { nameof(Supplier.Id) },
                supplier.FindPrimaryKey()!.Properties
                    .Select(property => property.Name).ToArray());

            var fields = new[]
            {
                (Name: nameof(Supplier.Code), Length: Supplier.MaxCodeLength, Nullable: false),
                (Name: nameof(Supplier.Name), Length: Supplier.MaxNameLength, Nullable: false),
                (Name: nameof(Supplier.ContactEmail), Length: Supplier.MaxContactEmailLength, Nullable: false),
                (Name: nameof(Supplier.PhoneNumber), Length: Supplier.MaxPhoneNumberLength, Nullable: true)
            };

            foreach (var field in fields)
            {
                var property = supplier.FindProperty(field.Name);
                Assert.NotNull(property);
                Assert.Equal(field.Length, property.GetMaxLength());
                Assert.Equal(field.Nullable, property.IsNullable);
            }

            Assert.False(supplier.FindProperty(nameof(Supplier.IsActive))!.IsNullable);

            var codeIndex = Assert.Single(supplier.GetIndexes(), index =>
                index.Properties.Count == 1 &&
                index.Properties[0].Name == nameof(Supplier.Code));

            Assert.True(codeIndex.IsUnique);
            Assert.Equal("IX_Suppliers_Code", codeIndex.GetDatabaseName());
            Assert.Null(codeIndex.GetFilter());
        }
    }
}
