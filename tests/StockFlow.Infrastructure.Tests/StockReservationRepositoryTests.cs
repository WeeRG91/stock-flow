using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using StockFlow.Infrastructure.Persistence;
using StockFlow.Infrastructure.Persistence.Repositories;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Tests;

public sealed class StockReservationRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SaveCreation_StagesReservationAndOnlyReservedBalanceInOneSave()
    {
        var requestId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var interceptor = new InspectSave((context, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            var reservation = Assert.Single(context.ChangeTracker.Entries<StockReservation>());
            Assert.Equal(EntityState.Added, reservation.State);
            Assert.Equal(requestId, reservation.Property<Guid>("RequestId").CurrentValue);
            Assert.NotEqual(Guid.Empty, reservation.Property<Guid>("Version").CurrentValue);
            var inventory = Assert.Single(context.ChangeTracker.Entries<InventoryEntity>());
            Assert.Equal(EntityState.Modified, inventory.State);
            Assert.Equal(reservation.Entity.InventoryId, inventory.Entity.Id);
            Assert.True(inventory.Property(item => item.QuantityReserved).IsModified);
            Assert.Equal(20, inventory.Property(item => item.QuantityReserved).OriginalValue);
            Assert.Equal(50, inventory.Property(item => item.QuantityReserved).CurrentValue);
            Assert.False(inventory.Property(item => item.QuantityOnHand).IsModified);
            Assert.Equal(100, inventory.Entity.QuantityOnHand);
            Assert.Equal(50, inventory.Entity.QuantityAvailable);
            Assert.Equal(7u, inventory.Property<uint>("Version").OriginalValue);
            Assert.Empty(context.ChangeTracker.Entries<StockMovement>());
            Assert.Empty(inventory.Entity.PendingMovements);
        });
        await using var context = CreateContext(interceptor);
        var inventory = PrepareInventory(context);
        var reservation = CreateReservation(inventory);
        inventory.ReserveStock(30);

        await new StockReservationRepository(context).SaveCreationAsync(
            reservation, inventory, requestId, cancellation.Token);

        Assert.Equal(1, interceptor.CallCount);
    }

    [Theory]
    [InlineData("concurrency", true)]
    [InlineData("requestUnique", true)]
    [InlineData("otherUnique", false)]
    [InlineData("otherSqlState", false)]
    [InlineData("unexpected", false)]
    [InlineData("cancelled", false)]
    public async Task SaveCreation_WhenSaveFails_TranslatesOnlyExpectedConflictsAndRejectsReuse(
        string failureKind, bool expectedConflict)
    {
        Exception failure = failureKind switch
        {
            "concurrency" => new DbUpdateConcurrencyException("Simulated stale inventory"),
            "requestUnique" => DatabaseFailure(PostgresErrorCodes.UniqueViolation, "IX_StockReservations_RequestId"),
            "otherUnique" => DatabaseFailure(PostgresErrorCodes.UniqueViolation, "OtherIndex"),
            "otherSqlState" => DatabaseFailure(PostgresErrorCodes.CheckViolation, "IX_StockReservations_RequestId"),
            "cancelled" => new OperationCanceledException("Simulated cancellation during save"),
            _ => new InvalidOperationException("Simulated failure")
        };
        var interceptor = new InspectSave((_, _) => throw failure);
        await using var context = CreateContext(interceptor);
        var inventory = PrepareInventory(context);
        var reservation = CreateReservation(inventory);
        inventory.ReserveStock(30);
        var repository = new StockReservationRepository(context);

        var error = await Record.ExceptionAsync(() =>
            repository.SaveCreationAsync(reservation, inventory, Guid.NewGuid()));

        if (expectedConflict)
        {
            Assert.Equal(reservation.Id, Assert.IsType<StockReservationConflictException>(error).ReservationId);
        }
        else
        {
            Assert.Same(failure, error);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SaveCreationAsync(reservation, inventory, Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.GetByRequestIdAsync(Guid.NewGuid()));
        Assert.Equal(1, interceptor.CallCount);
        Assert.Equal(EntityState.Added, context.Entry(reservation).State);
        Assert.Equal(50, inventory.QuantityReserved);
        Assert.Equal(100, inventory.QuantityOnHand);
        Assert.Empty(inventory.PendingMovements);
        // Interception proves staging and failure handling, not database rollback.
    }

    [Theory]
    [InlineData("detached")]
    [InlineData("added")]
    [InlineData("deleted")]
    public async Task SaveCreation_RejectsInventoryInInvalidTrackingState(string state)
    {
        var interceptor = new InspectSave();
        await using var context = CreateContext(interceptor);
        var inventory = PrepareInventory(context);
        var reservation = CreateReservation(inventory);
        inventory.ReserveStock(30);
        context.Entry(inventory).State = state switch
        {
            "detached" => EntityState.Detached,
            "added" => EntityState.Added,
            _ => EntityState.Deleted
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new StockReservationRepository(context).SaveCreationAsync(reservation, inventory, Guid.NewGuid()));
        Assert.Equal(0, interceptor.CallCount);
        Assert.Equal(EntityState.Detached, context.Entry(reservation).State);
    }

    [Theory]
    [InlineData("wrongInventory")]
    [InlineData("released")]
    [InlineData("fulfilled")]
    [InlineData("tracked")]
    [InlineData("wrongDelta")]
    [InlineData("physicalChange")]
    [InlineData("pendingMovement")]
    public async Task SaveCreation_RejectsInconsistentInputsBeforeSave(string invalid)
    {
        var interceptor = new InspectSave();
        await using var context = CreateContext(interceptor);
        var inventory = PrepareInventory(context);
        var reservation = invalid == "wrongInventory"
            ? new StockReservation(Guid.NewGuid(), 30, "Operation", Now, Guid.NewGuid())
            : CreateReservation(inventory);
        inventory.ReserveStock(invalid == "wrongDelta" ? 29 : 30);
        if (invalid == "released") reservation.Release(Now, Guid.NewGuid());
        if (invalid == "fulfilled") reservation.Fulfill(Now, Guid.NewGuid());
        if (invalid == "tracked") context.Attach(reservation);
        if (invalid is "physicalChange" or "pendingMovement")
        {
            inventory.ReceiveStock(1, "Unexpected receipt", Now);
            if (invalid == "physicalChange")
                inventory.AcknowledgePersistedMovements(inventory.PendingMovements.Select(item => item.Id).ToArray());
            else
                inventory.RemoveStock(1, "Unexpected removal", Now);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new StockReservationRepository(context).SaveCreationAsync(reservation, inventory, Guid.NewGuid()));
        Assert.Equal(0, interceptor.CallCount);
    }

    [Fact]
    public async Task SaveCreation_WithEmptyRequestId_RejectsBeforeStaging()
    {
        var interceptor = new InspectSave();
        await using var context = CreateContext(interceptor);
        var inventory = PrepareInventory(context);
        var reservation = CreateReservation(inventory);
        inventory.ReserveStock(30);
        await Assert.ThrowsAsync<ArgumentException>("requestId", () =>
            new StockReservationRepository(context).SaveCreationAsync(reservation, inventory, Guid.Empty));
        Assert.Equal(0, interceptor.CallCount);
        Assert.Equal(EntityState.Detached, context.Entry(reservation).State);
    }

    [Fact]
    public async Task SaveCreation_WithAmbientTransaction_RejectsBeforeStaging()
    {
        var interceptor = new InspectSave();
        await using var context = CreateContext(interceptor);
        var inventory = PrepareInventory(context);
        var reservation = CreateReservation(inventory);
        inventory.ReserveStock(30);
        using var transaction = new System.Transactions.TransactionScope(
            System.Transactions.TransactionScopeAsyncFlowOption.Enabled);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new StockReservationRepository(context).SaveCreationAsync(reservation, inventory, Guid.NewGuid()));
        Assert.Equal(0, interceptor.CallCount);
        Assert.Equal(EntityState.Detached, context.Entry(reservation).State);
    }

    [Fact]
    public async Task SaveCreation_WhenAlreadyCancelled_DoesNotStageOrSave()
    {
        var interceptor = new InspectSave();
        await using var context = CreateContext(interceptor);
        var inventory = PrepareInventory(context);
        var reservation = CreateReservation(inventory);
        inventory.ReserveStock(30);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new StockReservationRepository(context).SaveCreationAsync(
                reservation, inventory, Guid.NewGuid(), cancellation.Token));
        Assert.Equal(0, interceptor.CallCount);
        Assert.Equal(EntityState.Detached, context.Entry(reservation).State);
    }

    private static StockFlowDbContext CreateContext(SaveChangesInterceptor interceptor) =>
        new(new DbContextOptionsBuilder<StockFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=unused_reservation_test")
            .AddInterceptors(interceptor).Options);

    private static InventoryEntity PrepareInventory(StockFlowDbContext context)
    {
        var inventory = new InventoryEntity(Guid.NewGuid(), Guid.NewGuid());
        inventory.ReceiveStock(100, "Synthetic initial stock", Now);
        inventory.ReserveStock(20);
        inventory.AcknowledgePersistedMovements(inventory.PendingMovements.Select(item => item.Id).ToArray());
        var entry = context.Inventories.Attach(inventory);
        var version = entry.Property<uint>("Version");
        version.CurrentValue = 7u;
        version.OriginalValue = 7u;
        version.IsModified = false;
        return inventory;
    }

    private static StockReservation CreateReservation(InventoryEntity inventory) =>
        new(inventory.Id, 30, "Operation", Now, Guid.NewGuid());

    private static DbUpdateException DatabaseFailure(string state, string constraint) =>
        new("Simulated database failure", new PostgresException(
            messageText: "Simulated failure", severity: "ERROR", invariantSeverity: "ERROR",
            sqlState: state, constraintName: constraint));

    private sealed class InspectSave(Action<StockFlowDbContext, CancellationToken>? inspect = null)
        : SaveChangesInterceptor
    {
        public int CallCount { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var context = Assert.IsType<StockFlowDbContext>(eventData.Context);
            context.ChangeTracker.DetectChanges();
            inspect?.Invoke(context, cancellationToken);
            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(2));
        }
    }
}
