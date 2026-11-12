using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using StockFlow.Infrastructure.Persistence;
using StockFlow.Infrastructure.Persistence.Repositories;

namespace StockFlow.Infrastructure.Tests;

public class PurchaseOrderRepositoryTests
{
    [Fact]
    public async Task SaveAsync_WithNewOrder_StagesHeaderAndLinesTogether()
    {
        var order = new PurchaseOrder(
            "PO-TEST-001",
            Guid.NewGuid(),
            "SUP-001",
            "Supplier",
            Guid.NewGuid(),
            "BRU-01",
            "Warehouse",
            new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero),
            Guid.NewGuid());

        order.AddLine(Guid.NewGuid(), "LAPTOP-001", "Laptop", 2, 19.95m);
        order.AddLine(Guid.NewGuid(), "DOCK-001", "Dock", 1, 5.10m);

        var interceptor = new InspectNewOrderSaveInterceptor(order);
        var options = new DbContextOptionsBuilder<StockFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=unused_order_test")
            .AddInterceptors(interceptor)
            .Options;

        await using var context = new StockFlowDbContext(options);
        var repository = new PurchaseOrderRepository(context);

        await repository.SaveAsync(order);

        Assert.Equal(1, interceptor.CallCount);
    }

    [Fact]
    public async Task SaveAsync_WithTrackedSubmission_PreservesOriginalVersion()
    {
        var order = new PurchaseOrder(
            "PO-TEST-002",
            Guid.NewGuid(),
            "SUP-001",
            "Supplier",
            Guid.NewGuid(),
            "BRU-01",
            "Warehouse",
            new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero),
            Guid.NewGuid());

        order.AddLine(Guid.NewGuid(), "LAPTOP-001", "Laptop", 2, 19.95m);

        var originalVersion = Guid.NewGuid();
        var interceptor = new InspectSubmissionSaveInterceptor(originalVersion);
        var options = new DbContextOptionsBuilder<StockFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=unused_order_test")
            .AddInterceptors(interceptor)
            .Options;

        await using var context = new StockFlowDbContext(options);
        var entry = context.PurchaseOrders.Attach(order);
        var version = entry.Property<Guid>("Version");

        version.CurrentValue = originalVersion;
        version.OriginalValue = originalVersion;
        version.IsModified = false;

        order.Submit(order.CreatedAt.AddMinutes(1));

        var repository = new PurchaseOrderRepository(context);
        await repository.SaveAsync(order);

        Assert.Equal(1, interceptor.CallCount);
    }

    private sealed class InspectSubmissionSaveInterceptor(
        Guid originalVersion) : SaveChangesInterceptor
    {
        public int CallCount { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var context = Assert.IsType<StockFlowDbContext>(eventData.Context);
            var header = Assert.Single(context.ChangeTracker.Entries<PurchaseOrder>());
            var version = header.Property<Guid>("Version");

            Assert.Equal(EntityState.Modified, header.State);
            Assert.True(header.Property(order => order.Status).IsModified);
            Assert.True(version.IsModified);
            Assert.Equal(originalVersion, version.OriginalValue);
            Assert.NotEqual(originalVersion, version.CurrentValue);
            Assert.NotEqual(Guid.Empty, version.CurrentValue);

            var line = Assert.Single(context.ChangeTracker.Entries<PurchaseOrderLine>());
            Assert.Equal(EntityState.Unchanged, line.State);

            return ValueTask.FromResult(
                InterceptionResult<int>.SuppressWithResult(1));
        }
    }

    private sealed class InspectNewOrderSaveInterceptor(
        PurchaseOrder expectedOrder) : SaveChangesInterceptor
    {
        public int CallCount { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var context = Assert.IsType<StockFlowDbContext>(eventData.Context);

            var header = Assert.Single(context.ChangeTracker.Entries<PurchaseOrder>());
            Assert.Same(expectedOrder, header.Entity);
            Assert.Equal(EntityState.Added, header.State);
            Assert.NotEqual(Guid.Empty, header.Property<Guid>("Version").CurrentValue);

            var lines = context.ChangeTracker.Entries<PurchaseOrderLine>().ToArray();

            Assert.Equal(2, lines.Length);
            Assert.All(lines, line =>
            {
                Assert.Equal(EntityState.Added, line.State);
                Assert.Equal(
                    expectedOrder.Id,
                    line.Property<Guid>("PurchaseOrderId").CurrentValue);
            });

            return ValueTask.FromResult(
                InterceptionResult<int>.SuppressWithResult(1 + lines.Length));
        }
    }

    [Fact]
    public async Task SaveAsync_WithDraftLineEdits_StagesInsertUpdateAndDelete()
    {
        var order = new PurchaseOrder(
            "PO-TEST-003",
            Guid.NewGuid(),
            "SUP-001",
            "Supplier",
            Guid.NewGuid(),
            "BRU-01",
            "Warehouse",
            new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero),
            Guid.NewGuid());

        var updatedId = order.AddLine(Guid.NewGuid(), "LAPTOP", "Laptop", 2, 19.95m);
        var removedId = order.AddLine(Guid.NewGuid(), "DOCK", "Dock", 1, 5.10m);
        var originalLine = order.Lines.Single(line => line.Id == updatedId);
        var originalVersion = Guid.NewGuid();
        var interceptor = new InspectDraftEditInterceptor();

        var options = new DbContextOptionsBuilder<StockFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=unused_order_test")
            .AddInterceptors(interceptor)
            .Options;

        await using var context = new StockFlowDbContext(options);
        var version = context.PurchaseOrders.Attach(order).Property<Guid>("Version");
        version.CurrentValue = originalVersion;
        version.OriginalValue = originalVersion;
        version.IsModified = false;

        order.UpdateLine(updatedId, 3, 20m);
        order.RemoveLine(removedId);
        var addedId = order.AddLine(Guid.NewGuid(), "MOUSE", "Mouse", 1, 4m);

        await new PurchaseOrderRepository(context).SaveAsync(order);

        Assert.Equal(1, interceptor.CallCount);
        Assert.Equal(EntityState.Detached, context.Entry(originalLine).State);

        var entries = context.ChangeTracker.Entries<PurchaseOrderLine>()
            .ToDictionary(entry => entry.Entity.Id);

        Assert.Equal(3, entries.Count);
        Assert.Equal(EntityState.Added, entries[addedId].State);
        Assert.Equal(EntityState.Deleted, entries[removedId].State);

        var updated = entries[updatedId];
        Assert.Equal(EntityState.Modified, updated.State);
        Assert.Same(order.Lines.Single(line => line.Id == updatedId), updated.Entity);
        Assert.True(updated.Property(line => line.OrderedQuantity).IsModified);
        Assert.True(updated.Property(line => line.UnitPrice).IsModified);
        Assert.Equal(2, originalLine.OrderedQuantity);
        Assert.Equal(19.95m, originalLine.UnitPrice);

        Assert.Equal(order.Id, entries[addedId].Property<Guid>("PurchaseOrderId").CurrentValue);
        Assert.Equal(order.Id, updated.Property<Guid>("PurchaseOrderId").CurrentValue);
        Assert.Equal(originalVersion, version.OriginalValue);
        Assert.NotEqual(originalVersion, version.CurrentValue);
        Assert.True(version.IsModified);
    }

    [Fact]
    public async Task SaveAsync_WhenOrderVersionIsStale_TranslatesAndBlocksRetry()
    {
        var order = CreateConflictTestOrder();
        var interceptor = new ThrowingOrderSaveInterceptor(
            new DbUpdateConcurrencyException("Simulated stale order."));
        await using var context = CreateConflictTestContext(interceptor);
        context.PurchaseOrders.Attach(order);

        var repository = new PurchaseOrderRepository(context);

        var conflict = await Assert.ThrowsAsync<PurchaseOrderConflictException>(
            () => repository.SaveAsync(order));

        Assert.Equal(order.Id, conflict.PurchaseOrderId);
        Assert.Contains("Reload", conflict.Message);

        var retry = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.SaveAsync(order));

        Assert.Contains("Discard this scope", retry.Message);
        Assert.Equal(1, interceptor.CallCount);
    }

    [Theory]
    [InlineData(PostgresErrorCodes.UniqueViolation,
        "IX_PurchaseOrders_OrderNumber", true)]
    [InlineData(PostgresErrorCodes.UniqueViolation,
        "IX_PurchaseOrderLines_PurchaseOrderId_ProductId", true)]
    [InlineData(PostgresErrorCodes.UniqueViolation,
        "IX_Unrelated_UniqueIndex", false)]
    [InlineData(PostgresErrorCodes.CheckViolation,
        "IX_PurchaseOrders_OrderNumber", false)]
    public async Task SaveAsync_OnlyTranslatesKnownUniqueViolations(
        string sqlState,
        string constraintName,
        bool expectedConflict)
    {
        var order = CreateConflictTestOrder();
        var postgresFailure = new PostgresException(
            messageText: "Simulated database failure.",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: sqlState,
            constraintName: constraintName);

        var databaseFailure = new DbUpdateException(
            "Simulated save failure.", postgresFailure);

        var interceptor = new ThrowingOrderSaveInterceptor(databaseFailure);
        await using var context = CreateConflictTestContext(interceptor);
        var repository = new PurchaseOrderRepository(context);

        if (expectedConflict)
        {
            var conflict = await Assert.ThrowsAsync<PurchaseOrderConflictException>(
                () => repository.SaveAsync(order));

            Assert.Equal(order.Id, conflict.PurchaseOrderId);
        }
        else
        {
            var failure = await Assert.ThrowsAsync<DbUpdateException>(
                () => repository.SaveAsync(order));

            Assert.Same(databaseFailure, failure);
        }

        Assert.Equal(1, interceptor.CallCount);
    }

    private static PurchaseOrder CreateConflictTestOrder()
    {
        return new PurchaseOrder(
            "PO-TEST-CONFLICT",
            Guid.NewGuid(), "SUP-001", "Supplier",
            Guid.NewGuid(), "BRU-01", "Warehouse",
            new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero),
            Guid.NewGuid());
    }

    private static StockFlowDbContext CreateConflictTestContext(
        SaveChangesInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<StockFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=unused_order_test")
            .AddInterceptors(interceptor)
            .Options;

        return new StockFlowDbContext(options);
    }

    private sealed class ThrowingOrderSaveInterceptor(
        Exception failure) : SaveChangesInterceptor
    {
        public int CallCount { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw failure;
        }
    }

    private sealed class InspectDraftEditInterceptor : SaveChangesInterceptor
    {
        public int CallCount { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(4));
        }
    }
}
