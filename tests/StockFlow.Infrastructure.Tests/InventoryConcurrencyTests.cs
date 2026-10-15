using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using StockFlow.Application.Exceptions;
using StockFlow.Infrastructure.Persistence;
using StockFlow.Infrastructure.Persistence.Repositories;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Tests
{
    public class InventoryConcurrencyTests
    {
        [Fact]
        public void InventoryVersion_IsShadowConcurrencyTokenMappedToXmin()
        {
            var options =
                new DbContextOptionsBuilder<StockFlowDbContext>()
                    .UseNpgsql("Host=localhost;Database=unused_model_test")
                    .Options;

            using var context = new StockFlowDbContext(options);

            var entity = context.Model.FindEntityType(typeof(InventoryEntity));
            Assert.NotNull(entity);

            var version = entity.FindProperty("Version");
            Assert.NotNull(version);

            Assert.Equal(typeof(uint), version.ClrType);
            Assert.True(version.IsShadowProperty());
            Assert.True(version.IsConcurrencyToken);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, version.ValueGenerated);

            var table = StoreObjectIdentifier.Table(
                entity.GetTableName()!,
                entity.GetSchema());

            Assert.Equal("xmin", version.GetColumnName(table));
        }

        [Fact]
        public async Task SaveAsync_WhenConcurrencyFails_TranslatesAndPreservesPendingMovements()
        {
            var interceptor = new ConcurrencyFailureInterceptor();

            var options =
                new DbContextOptionsBuilder<StockFlowDbContext>()
                    .UseNpgsql("Host=localhost;Database=unused_conflict_test")
                    .AddInterceptors(interceptor)
                    .Options;

            await using var context = new StockFlowDbContext(options);

            var inventory = new InventoryEntity(
                Guid.NewGuid(),
                Guid.NewGuid());

            // Simulate an existing loaded inventory.
            context.Inventories.Attach(inventory);

            inventory.ReceiveStock(
                5,
                "Receipt",
                new DateTimeOffset(
                    2026, 10, 15, 10, 0, 0, TimeSpan.Zero));

            var movementsBefore = inventory.PendingMovements.ToArray();
            var repository = new InventoryRepository(context);

            var conflict = await Assert.ThrowsAsync<InventoryConflictException>(
                () => repository.SaveAsync(inventory));

            Assert.Equal(inventory.ProductId, conflict.ProductId);
            Assert.Equal(inventory.WarehouseId, conflict.WarehouseId);
            Assert.Equal(5, inventory.QuantityOnHand);
            Assert.Equal(
                movementsBefore,
                inventory.PendingMovements.ToArray());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.SaveAsync(inventory));

            Assert.Equal(1, interceptor.SaveCallCount);
        }

        [Theory]
        [InlineData("23505", "IX_Inventories_ProductId_WarehouseId", true)]
        [InlineData("23505", "PK_StockMovements", false)]
        [InlineData("23514", "CK_Inventories_QuantityOnHand_NonNegative", false)]
        public async Task SaveAsync_TranslatesOnlyInventoryPairUniqueViolation(
    string sqlState,
    string constraintName,
    bool expectedConflict)
        {
            // Arrange
            var postgresException = new PostgresException(
                messageText: "Simulated database constraint failure.",
                severity: "ERROR",
                invariantSeverity: "ERROR",
                sqlState: sqlState,
                constraintName: constraintName);

            var databaseException = new DbUpdateException(
                "Simulated save failure.",
                postgresException);

            var interceptor = new DatabaseFailureInterceptor(databaseException);

            var options =
                new DbContextOptionsBuilder<StockFlowDbContext>()
                    .UseNpgsql("Host=localhost;Database=unused_filter_test")
                    .AddInterceptors(interceptor)
                    .Options;

            await using var context = new StockFlowDbContext(options);

            var inventory = new InventoryEntity(
                Guid.NewGuid(),
                Guid.NewGuid());

            inventory.ReceiveStock(
                5,
                "Receipt",
                new DateTimeOffset(
                    2026, 10, 15, 10, 0, 0, TimeSpan.Zero));

            var movementsBefore = inventory.PendingMovements.ToArray();
            var repository = new InventoryRepository(context);

            // Act and assert
            if (expectedConflict)
            {
                var conflict = await Assert.ThrowsAsync<InventoryConflictException>(
                    () => repository.SaveAsync(inventory));

                Assert.Equal(inventory.ProductId, conflict.ProductId);
                Assert.Equal(inventory.WarehouseId, conflict.WarehouseId);
            }
            else
            {
                var failure = await Assert.ThrowsAsync<DbUpdateException>(
                    () => repository.SaveAsync(inventory));

                Assert.Same(databaseException, failure);
            }

            Assert.Equal(5, inventory.QuantityOnHand);
            Assert.Equal(movementsBefore, inventory.PendingMovements.ToArray());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.SaveAsync(inventory));

            Assert.Equal(1, interceptor.SaveCallCount);
        }

        private sealed class ConcurrencyFailureInterceptor
            : SaveChangesInterceptor
        {
            public int SaveCallCount { get; private set; }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
            {
                SaveCallCount++;

                throw new DbUpdateConcurrencyException(
                    "Simulated stale inventory update.");
            }
        }

        private sealed class DatabaseFailureInterceptor : SaveChangesInterceptor
        {
            private readonly DbUpdateException _exception;

            public int SaveCallCount { get; private set; }

            public DatabaseFailureInterceptor(DbUpdateException exception)
            {
                _exception = exception;
            }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
            {
                SaveCallCount++;
                throw _exception;
            }
        }
    }
}