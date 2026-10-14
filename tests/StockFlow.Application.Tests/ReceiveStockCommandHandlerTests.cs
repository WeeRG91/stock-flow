using StockFlow.Application.Inventory.ReceiveStock;
using StockFlow.Application.Tests.Fakes;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Tests
{
    public class ReceiveStockCommandHandlerTests
    {
        private static (
            ReceiveStockCommandHandler Handler,
            Product Product,
            Warehouse Warehouse,
            FakeProductRepository Products,
            FakeWarehouseRepository Warehouses,
            FakeInventoryRepository Inventories,
            FakeClock Clock) CreateSetup()
        {
            var product = new Product("Office Chair", "CHAIR-BLK");
            var warehouse = new Warehouse(
                "BRU-01",
                "Brussels Warehouse",
                "Brussels, Belgium");

            var products = new FakeProductRepository();
            products.Add(product);

            var warehouses = new FakeWarehouseRepository();
            warehouses.Add(warehouse);

            var inventories = new FakeInventoryRepository();
            var clock = new FakeClock();

            var handler = new ReceiveStockCommandHandler(
                products, warehouses, inventories, clock);

            return (
                handler, product, warehouse,
                products, warehouses, inventories, clock);
        }

        [Fact]
        public async Task HandleAsync_WhenInventoryDoesNotExist_CreatesAndSavesInventory()
        {
            // Arrange
            var product = new Product("Office Chair", "CHAIR-BLK");
            var warehouse = new Warehouse(
                "BRU-01",
                "Brussels Warehouse",
                "Brussels, Belgium");

            var products = new FakeProductRepository();
            products.Add(product);

            var warehouses = new FakeWarehouseRepository();
            warehouses.Add(warehouse);

            var inventories = new FakeInventoryRepository();
            var clock = new FakeClock();

            var handler = new ReceiveStockCommandHandler(
                products,
                warehouses,
                inventories,
                clock);

            var command = new ReceiveStockCommand(
                product.Id,
                warehouse.Id,
                10);

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.Equal(1, inventories.GetCallCount);
            Assert.Equal(1, inventories.SaveCallCount);

            var savedInventory = Assert.IsType<InventoryEntity>(
                inventories.LastSavedInventory);

            Assert.Equal(product.Id, savedInventory.ProductId);
            Assert.Equal(warehouse.Id, savedInventory.WarehouseId);
            Assert.Equal(10, savedInventory.QuantityOnHand);
            Assert.Equal(savedInventory.Id, result.InventoryId);
            Assert.Equal(10, result.QuantityOnHand);

            var movement = Assert.Single(savedInventory.PendingMovements);

            Assert.Equal(savedInventory.Id, movement.InventoryId);
            Assert.Equal(10, movement.QuantityChange);
            Assert.Equal(StockMovementType.Receipt, movement.Type);
            Assert.Equal("Manual stock receipt", movement.Reason);
            Assert.Equal(clock.UtcNow, movement.OccurredAt);
        }

        [Fact]
        public async Task HandleAsync_WhenInventoryExists_UpdatesAndSavesExistingInventory()
        {
            // Arrange
            var product = new Product("Office Chair", "CHAIR-BLK");
            var warehouse = new Warehouse(
                "BRU-01",
                "Brussels Warehouse",
                "Brussels, Belgium");

            var products = new FakeProductRepository();
            products.Add(product);

            var warehouses = new FakeWarehouseRepository();
            warehouses.Add(warehouse);

            var clock = new FakeClock();
            var inventory = new InventoryEntity(product.Id, warehouse.Id);
            inventory.ReceiveStock(
                10,
                "Initial stock",
                clock.UtcNow.AddDays(-1));

            var initialMovement = Assert.Single(inventory.PendingMovements);

            var inventories = new FakeInventoryRepository();
            inventories.Add(inventory);

            var handler = new ReceiveStockCommandHandler(
                products,
                warehouses,
                inventories,
                clock);

            var command = new ReceiveStockCommand(
                product.Id,
                warehouse.Id,
                5);

            // Act
            var result = await handler.HandleAsync(command);

            // Assert
            Assert.Equal(1, inventories.GetCallCount);
            Assert.Equal(1, inventories.SaveCallCount);
            Assert.Same(inventory, inventories.LastSavedInventory);

            Assert.Equal(inventory.Id, result.InventoryId);
            Assert.Equal(15, result.QuantityOnHand);
            Assert.Equal(15, inventory.QuantityOnHand);

            Assert.Equal(2, inventory.PendingMovements.Count);
            Assert.Same(initialMovement, inventory.PendingMovements[0]);

            var movement = inventory.PendingMovements[1];

            Assert.Equal(inventory.Id, movement.InventoryId);
            Assert.Equal(5, movement.QuantityChange);
            Assert.Equal(StockMovementType.Receipt, movement.Type);
            Assert.Equal("Manual stock receipt", movement.Reason);
            Assert.Equal(clock.UtcNow, movement.OccurredAt);
        }

        [Fact]
        public async Task HandleAsync_WithNullCommand_RejectsBeforeDataAccess()
        {
            var setup = CreateSetup();

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => setup.Handler.HandleAsync(null!));

            Assert.Equal(0, setup.Products.GetByIdCallCount);
            Assert.Equal(0, setup.Warehouses.GetByIdCallCount);
            Assert.Equal(0, setup.Inventories.GetCallCount);
            Assert.Equal(0, setup.Inventories.SaveCallCount);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task HandleAsync_WithEmptyIdentifier_RejectsBeforeDataAccess(
            bool emptyProductId)
        {
            var setup = CreateSetup();
            var command = new ReceiveStockCommand(
                emptyProductId ? Guid.Empty : setup.Product.Id,
                emptyProductId ? setup.Warehouse.Id : Guid.Empty,
                10);

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => setup.Handler.HandleAsync(command));

            Assert.Equal(
                emptyProductId ? "ProductId" : "WarehouseId",
                exception.ParamName);
            Assert.Equal(0, setup.Products.GetByIdCallCount);
            Assert.Equal(0, setup.Warehouses.GetByIdCallCount);
            Assert.Equal(0, setup.Inventories.GetCallCount);
            Assert.Equal(0, setup.Inventories.SaveCallCount);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task HandleAsync_WithInvalidQuantity_RejectsBeforeDataAccess(
            int quantity)
        {
            var setup = CreateSetup();
            var command = new ReceiveStockCommand(
                setup.Product.Id, setup.Warehouse.Id, quantity);

            var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => setup.Handler.HandleAsync(command));

            Assert.Equal("Quantity", exception.ParamName);
            Assert.Equal(0, setup.Products.GetByIdCallCount);
            Assert.Equal(0, setup.Warehouses.GetByIdCallCount);
            Assert.Equal(0, setup.Inventories.GetCallCount);
            Assert.Equal(0, setup.Inventories.SaveCallCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task HandleAsync_WithMissingOrInactiveProduct_DoesNotAccessInventory(
            bool inactive)
        {
            var setup = CreateSetup();

            if (inactive)
            {
                setup.Product.Deactivate();
            }

            var command = new ReceiveStockCommand(
                inactive ? setup.Product.Id : Guid.NewGuid(),
                setup.Warehouse.Id,
                10);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => setup.Handler.HandleAsync(command));

            Assert.Equal(1, setup.Products.GetByIdCallCount);
            Assert.Equal(0, setup.Warehouses.GetByIdCallCount);
            Assert.Equal(0, setup.Inventories.GetCallCount);
            Assert.Equal(0, setup.Inventories.SaveCallCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task HandleAsync_WithMissingOrInactiveWarehouse_DoesNotAccessInventory(
            bool inactive)
        {
            var setup = CreateSetup();

            if (inactive)
            {
                setup.Warehouse.Deactivate();
            }

            var command = new ReceiveStockCommand(
                setup.Product.Id,
                inactive ? setup.Warehouse.Id : Guid.NewGuid(),
                10);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => setup.Handler.HandleAsync(command));

            Assert.Equal(1, setup.Products.GetByIdCallCount);
            Assert.Equal(1, setup.Warehouses.GetByIdCallCount);
            Assert.Equal(0, setup.Inventories.GetCallCount);
            Assert.Equal(0, setup.Inventories.SaveCallCount);
        }

        [Fact]
        public async Task HandleAsync_WhenStockOverflows_PreservesStateAndDoesNotSave()
        {
            var setup = CreateSetup();
            var inventory = new InventoryEntity(
                setup.Product.Id, setup.Warehouse.Id);

            inventory.ReceiveStock(
                int.MaxValue, "Initial stock", setup.Clock.UtcNow);
            setup.Inventories.Add(inventory);

            var movementsBefore = inventory.PendingMovements.ToArray();
            var command = new ReceiveStockCommand(
                setup.Product.Id, setup.Warehouse.Id, 1);

            await Assert.ThrowsAsync<OverflowException>(
                () => setup.Handler.HandleAsync(command));

            Assert.Equal(int.MaxValue, inventory.QuantityOnHand);
            Assert.Equal(movementsBefore, inventory.PendingMovements.ToArray());
            Assert.Equal(0, setup.Inventories.SaveCallCount);
            Assert.Null(setup.Inventories.LastSavedInventory);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task HandleAsync_WithInvalidClockTime_PreservesStateAndDoesNotSave(
            bool useDefaultTimestamp)
        {
            var setup = CreateSetup();
            var inventory = new InventoryEntity(
                setup.Product.Id, setup.Warehouse.Id);

            inventory.ReceiveStock(10, "Initial stock", setup.Clock.UtcNow);
            setup.Inventories.Add(inventory);
            var movementsBefore = inventory.PendingMovements.ToArray();

            setup.Clock.UtcNow = useDefaultTimestamp
                ? default
                : setup.Clock.UtcNow.ToOffset(TimeSpan.FromHours(2));

            var command = new ReceiveStockCommand(
                setup.Product.Id, setup.Warehouse.Id, 1);

            // The default timestamp throws ArgumentOutOfRangeException,
            // which derives from ArgumentException.
            await Assert.ThrowsAnyAsync<ArgumentException>(
                () => setup.Handler.HandleAsync(command));

            Assert.Equal(10, inventory.QuantityOnHand);
            Assert.Equal(movementsBefore, inventory.PendingMovements.ToArray());
            Assert.Equal(0, setup.Inventories.SaveCallCount);
        }

        [Fact]
        public async Task HandleAsync_WhenAlreadyCancelled_DoesNotAccessRepositories()
        {
            var setup = CreateSetup();
            var command = new ReceiveStockCommand(
                setup.Product.Id, setup.Warehouse.Id, 10);

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => setup.Handler.HandleAsync(command, cancellation.Token));

            Assert.Equal(0, setup.Products.GetByIdCallCount);
            Assert.Equal(0, setup.Warehouses.GetByIdCallCount);
            Assert.Equal(0, setup.Inventories.GetCallCount);
            Assert.Equal(0, setup.Inventories.SaveCallCount);
        }
    }
}
