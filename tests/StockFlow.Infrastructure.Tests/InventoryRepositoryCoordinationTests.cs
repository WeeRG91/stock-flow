using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StockFlow.Domain.Entities;
using StockFlow.Infrastructure.Persistence;
using StockFlow.Infrastructure.Persistence.Repositories;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Tests
{
    public class InventoryRepositoryCoordinationTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SaveAsync_WhenPersistenceFails_StagesChangesAndPreservesPendingMovements(
            bool existingInventory)
        {
            // Arrange
            var interceptor = new FailingSaveInterceptor();

            var options =
                new DbContextOptionsBuilder<StockFlowDbContext>()
                    .UseNpgsql(
                        "Host=localhost;Database=unused_coordination_test")
                    .AddInterceptors(interceptor)
                    .Options;

            await using var context = new StockFlowDbContext(options);

            var inventory = new InventoryEntity(
                Guid.NewGuid(),
                Guid.NewGuid());

            var occurredAt = new DateTimeOffset(
                2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

            if (existingInventory)
            {
                inventory.ReceiveStock(10, "Initial stock", occurredAt);

                // Simulate an already-persisted entity.
                var previousIds = inventory.PendingMovements
                    .Select(movement => movement.Id)
                    .ToArray();

                inventory.AcknowledgePersistedMovements(previousIds);
                context.Inventories.Attach(inventory);
            }

            inventory.ReceiveStock(5, "New receipt", occurredAt);
            inventory.ReceiveStock(2, "Another receipt", occurredAt);

            var movementsBefore = inventory.PendingMovements.ToArray();
            var quantityBefore = inventory.QuantityOnHand;
            var repository = new InventoryRepository(context);

            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.SaveAsync(inventory));

            // Assert
            Assert.Equal("Simulated persistence failure.", exception.Message);
            Assert.Equal(1, interceptor.SaveCallCount);

            Assert.Equal(
                existingInventory
                    ? EntityState.Modified
                    : EntityState.Added,
                interceptor.InventoryState);

            Assert.Equal(2, interceptor.AddedMovementCount);
            Assert.Equal(quantityBefore, inventory.QuantityOnHand);
            Assert.Equal(
                movementsBefore,
                inventory.PendingMovements.ToArray());

            // The repository must reject reuse after failure.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.SaveAsync(inventory));

            Assert.Equal(1, interceptor.SaveCallCount);
            Assert.Equal(
                movementsBefore,
                inventory.PendingMovements.ToArray());
        }

        private sealed class FailingSaveInterceptor : SaveChangesInterceptor
        {
            public int SaveCallCount { get; private set; }
            public EntityState InventoryState { get; private set; }
            public int AddedMovementCount { get; private set; }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
            {
                var context = eventData.Context
                    ?? throw new InvalidOperationException(
                        "Expected a DbContext.");

                context.ChangeTracker.DetectChanges();

                SaveCallCount++;

                InventoryState = context.ChangeTracker
                    .Entries<InventoryEntity>()
                    .Single()
                    .State;

                AddedMovementCount = context.ChangeTracker
                    .Entries<StockMovement>()
                    .Count(entry => entry.State == EntityState.Added);

                throw new InvalidOperationException(
                    "Simulated persistence failure.");
            }
        }
    }
}