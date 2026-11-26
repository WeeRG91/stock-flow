using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using StockFlow.Infrastructure.Persistence;
using StockFlow.Infrastructure.Persistence.Repositories;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Tests;

public sealed class StockReservationFulfillmentRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Fulfillment_StagesAllThreeChangesAndAcknowledgesOnlyAfterSave()
    {
        var version = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var interceptor = new InspectSave((context, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            var reservation = Assert.Single(context.ChangeTracker.Entries<StockReservation>());
            Assert.Equal(EntityState.Modified, reservation.State);
            Assert.Equal(StockReservationStatus.Active, reservation.Property(item => item.Status).OriginalValue);
            Assert.Equal(StockReservationStatus.Fulfilled, reservation.Entity.Status);
            Assert.Equal(version, reservation.Property<Guid>("Version").OriginalValue);
            Assert.NotEqual(version, reservation.Property<Guid>("Version").CurrentValue);
            Assert.NotEqual(Guid.Empty, reservation.Property<Guid>("Version").CurrentValue);
            Assert.Equal(requestId, reservation.Property<Guid>("RequestId").CurrentValue);
            Assert.False(reservation.Property<Guid>("RequestId").IsModified);
            var inventory = Assert.Single(context.ChangeTracker.Entries<InventoryEntity>());
            Assert.Equal(EntityState.Modified, inventory.State);
            Assert.Equal(100, inventory.Property(item => item.QuantityOnHand).OriginalValue);
            Assert.Equal(70, inventory.Property(item => item.QuantityOnHand).CurrentValue);
            Assert.Equal(50, inventory.Property(item => item.QuantityReserved).OriginalValue);
            Assert.Equal(20, inventory.Property(item => item.QuantityReserved).CurrentValue);
            Assert.True(inventory.Property(item => item.QuantityOnHand).IsModified);
            Assert.True(inventory.Property(item => item.QuantityReserved).IsModified);
            Assert.Equal(7u, inventory.Property<uint>("Version").OriginalValue);
            var movement = Assert.Single(context.ChangeTracker.Entries<StockMovement>());
            Assert.Equal(EntityState.Added, movement.State);
            Assert.Same(movement.Entity, Assert.Single(inventory.Entity.PendingMovements));
            Assert.Equal(inventory.Entity.Id, movement.Entity.InventoryId);
            Assert.Equal(-30, movement.Entity.QuantityChange);
            Assert.Equal(StockMovementType.Removal, movement.Entity.Type);
            Assert.Equal(reservation.Entity.FulfilledAt, movement.Entity.OccurredAt);
        });
        await using var context = CreateContext(interceptor);
        var (inventory, reservation) = Prepare(context, version, requestId);
        Fulfill(inventory, reservation);

        await new StockReservationRepository(context).SaveFulfillmentAsync(reservation, inventory, cancellation.Token);

        Assert.Equal(1, interceptor.CallCount);
        Assert.Empty(inventory.PendingMovements);
        Assert.Equal(50, inventory.QuantityAvailable);
    }

    [Fact]
    public async Task Fulfillment_SavingAgainWithoutNewMovement_DoesNotInsertDuplicate()
    {
        var interceptor = new InspectSave();
        await using var context = CreateContext(interceptor);
        var (inventory, reservation) = Prepare(context);
        Fulfill(inventory, reservation);
        var repository = new StockReservationRepository(context);
        await repository.SaveFulfillmentAsync(reservation, inventory);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveFulfillmentAsync(reservation, inventory));
        Assert.Equal(1, interceptor.CallCount);
        Assert.Single(context.ChangeTracker.Entries<StockMovement>());
        Assert.Empty(inventory.PendingMovements);
        Assert.Equal(70, inventory.QuantityOnHand);
        Assert.Equal(20, inventory.QuantityReserved);
    }

    [Theory]
    [InlineData("concurrency")]
    [InlineData("database")]
    [InlineData("unexpected")]
    [InlineData("cancelled")]
    public async Task Fulfillment_WhenSaveFails_KeepsPendingMovementAndRejectsReuse(string kind)
    {
        Exception failure = kind switch
        {
            "concurrency" => new DbUpdateConcurrencyException("Simulated stale data"),
            "database" => new DbUpdateException("Unrelated database error"),
            "cancelled" => new OperationCanceledException("Simulated save cancellation"),
            _ => new InvalidOperationException("Simulated failure")
        };
        var interceptor = new InspectSave((_, _) => throw failure);
        await using var context = CreateContext(interceptor);
        var (inventory, reservation) = Prepare(context);
        Fulfill(inventory, reservation);
        var movement = Assert.Single(inventory.PendingMovements);
        var repository = new StockReservationRepository(context);

        var error = await Record.ExceptionAsync(() => repository.SaveFulfillmentAsync(reservation, inventory));

        if (kind == "concurrency")
            Assert.Equal(reservation.Id, Assert.IsType<StockReservationConflictException>(error).ReservationId);
        else
            Assert.Same(failure, error);
        Assert.Same(movement, Assert.Single(inventory.PendingMovements));
        Assert.Equal(EntityState.Added, context.Entry(movement).State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveFulfillmentAsync(reservation, inventory));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.GetByIdAsync(reservation.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.GetByRequestIdAsync(Guid.NewGuid()));
        Assert.Equal(1, interceptor.CallCount);
        Assert.Equal(100, context.Entry(inventory).Property(item => item.QuantityOnHand).OriginalValue);
        Assert.Equal(50, context.Entry(inventory).Property(item => item.QuantityReserved).OriginalValue);
        // Suppressed/failed saves test coordination, not actual PostgreSQL transaction rollback.
    }

    [Theory]
    [InlineData("reservationDetached")]
    [InlineData("reservationAdded")]
    [InlineData("reservationDeleted")]
    [InlineData("inventoryDetached")]
    [InlineData("inventoryAdded")]
    [InlineData("inventoryDeleted")]
    [InlineData("stillActive")]
    [InlineData("released")]
    [InlineData("alreadyFulfilled")]
    [InlineData("onlyReservedChanged")]
    [InlineData("onlyOnHandChanged")]
    [InlineData("wrongQuantity")]
    [InlineData("missingMovement")]
    [InlineData("extraMovements")]
    [InlineData("wrongTimestamp")]
    [InlineData("trackedMovement")]
    public async Task Fulfillment_WithInvalidTransitionBalanceOrMovement_DoesNotSave(string invalid)
    {
        var interceptor = new InspectSave();
        await using var context = CreateContext(interceptor);
        var (inventory, reservation) = Prepare(context);
        if (invalid == "released") reservation.Release(Now, Guid.NewGuid());
        else if (invalid != "stillActive") reservation.Fulfill(Now, Guid.NewGuid());
        if (invalid == "onlyReservedChanged") inventory.ReleaseReservedStock(30);
        else if (invalid == "onlyOnHandChanged") inventory.RemoveStock(30, "Other removal", Now);
        else inventory.ConsumeReservedStock(invalid == "wrongQuantity" ? 29 : 30,
            "Fulfillment", invalid == "wrongTimestamp" ? Now.AddMinutes(1) : Now);
        if (invalid == "alreadyFulfilled")
            context.Entry(reservation).Property(item => item.Status).OriginalValue = StockReservationStatus.Fulfilled;
        if (invalid == "missingMovement")
            inventory.AcknowledgePersistedMovements(inventory.PendingMovements.Select(item => item.Id).ToArray());
        if (invalid == "extraMovements")
        {
            inventory.ReceiveStock(1, "Other receipt", Now);
            inventory.RemoveStock(1, "Other removal", Now);
        }
        if (invalid == "trackedMovement") context.StockMovements.Add(Assert.Single(inventory.PendingMovements));
        if (invalid.StartsWith("reservation", StringComparison.Ordinal))
            context.Entry(reservation).State = invalid switch
            {
                "reservationDetached" => EntityState.Detached,
                "reservationAdded" => EntityState.Added,
                _ => EntityState.Deleted
            };
        if (invalid == "inventoryDeleted")
            context.ChangeTracker.CascadeDeleteTiming = Microsoft.EntityFrameworkCore.ChangeTracking.CascadeTiming.Never;
        if (invalid.StartsWith("inventory", StringComparison.Ordinal))
            context.Entry(inventory).State = invalid switch
            {
                "inventoryDetached" => EntityState.Detached,
                "inventoryAdded" => EntityState.Added,
                _ => EntityState.Deleted
            };
        var pending = inventory.PendingMovements.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new StockReservationRepository(context).SaveFulfillmentAsync(reservation, inventory));

        Assert.Equal(0, interceptor.CallCount);
        Assert.Equal(pending, inventory.PendingMovements.ToArray());
    }

    [Fact]
    public async Task Fulfillment_WhenAlreadyCancelled_DoesNotStageMovement()
    {
        var interceptor = new InspectSave();
        await using var context = CreateContext(interceptor);
        var (inventory, reservation) = Prepare(context);
        Fulfill(inventory, reservation);
        var movement = Assert.Single(inventory.PendingMovements);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new StockReservationRepository(context).SaveFulfillmentAsync(reservation, inventory, cancellation.Token));
        Assert.Equal(0, interceptor.CallCount);
        Assert.Equal(EntityState.Detached, context.Entry(movement).State);
        Assert.Same(movement, Assert.Single(inventory.PendingMovements));
    }

    [Fact]
    public async Task Fulfillment_WithAmbientTransaction_DoesNotStageMovement()
    {
        var interceptor = new InspectSave();
        await using var context = CreateContext(interceptor);
        var (inventory, reservation) = Prepare(context);
        Fulfill(inventory, reservation);
        var movement = Assert.Single(inventory.PendingMovements);
        using var transaction = new System.Transactions.TransactionScope(
            System.Transactions.TransactionScopeAsyncFlowOption.Enabled);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new StockReservationRepository(context).SaveFulfillmentAsync(reservation, inventory));
        Assert.Equal(0, interceptor.CallCount);
        Assert.Equal(EntityState.Detached, context.Entry(movement).State);
        Assert.Same(movement, Assert.Single(inventory.PendingMovements));
    }

    private static void Fulfill(InventoryEntity inventory, StockReservation reservation)
    {
        reservation.Fulfill(Now, Guid.NewGuid());
        inventory.ConsumeReservedStock(30, $"Stock reservation {reservation.Id} fulfilled", Now);
    }

    private static (InventoryEntity Inventory, StockReservation Reservation) Prepare(
        StockFlowDbContext context, Guid? version = null, Guid? requestId = null)
    {
        var inventory = new InventoryEntity(Guid.NewGuid(), Guid.NewGuid());
        inventory.ReceiveStock(100, "Synthetic stock", Now);
        inventory.ReserveStock(50);
        inventory.AcknowledgePersistedMovements(inventory.PendingMovements.Select(item => item.Id).ToArray());
        var inventoryVersion = context.Inventories.Attach(inventory).Property<uint>("Version");
        inventoryVersion.CurrentValue = 7u;
        inventoryVersion.OriginalValue = 7u;
        inventoryVersion.IsModified = false;
        var reservation = new StockReservation(inventory.Id, 30, "Operation", Now.AddHours(-1), Guid.NewGuid());
        var entry = context.StockReservations.Attach(reservation);
        foreach (var (name, value) in new[] { ("Version", version ?? Guid.NewGuid()), ("RequestId", requestId ?? Guid.NewGuid()) })
        {
            entry.Property<Guid>(name).CurrentValue = value;
            entry.Property<Guid>(name).OriginalValue = value;
            entry.Property<Guid>(name).IsModified = false;
        }
        return (inventory, reservation);
    }

    private static StockFlowDbContext CreateContext(SaveChangesInterceptor interceptor) =>
        new(new DbContextOptionsBuilder<StockFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=unused_reservation_fulfillment_test")
            .AddInterceptors(interceptor).Options);

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
            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(3));
        }
    }
}
