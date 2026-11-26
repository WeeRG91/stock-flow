using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using StockFlow.Infrastructure.Persistence;
using StockFlow.Infrastructure.Persistence.Repositories;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Tests;

public sealed class StockReservationReleaseRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Release_SavesBothTrackedChangesAndAdvancesOnlyReservationVersion()
    {
        using var cancellation = new CancellationTokenSource();
        var version = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var interceptor = new InspectSave((context, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            var reservation = Assert.Single(context.ChangeTracker.Entries<StockReservation>());
            Assert.Equal(EntityState.Modified, reservation.State);
            Assert.Equal(StockReservationStatus.Active, reservation.Property(item => item.Status).OriginalValue);
            Assert.Equal(StockReservationStatus.Released, reservation.Entity.Status);
            Assert.Equal(actorId, reservation.Entity.ReleasedByUserId);
            Assert.Equal(Now, reservation.Entity.ReleasedAt);
            Assert.Equal(version, reservation.Property<Guid>("Version").OriginalValue);
            Assert.NotEqual(version, reservation.Property<Guid>("Version").CurrentValue);
            Assert.NotEqual(Guid.Empty, reservation.Property<Guid>("Version").CurrentValue);
            Assert.Equal(requestId, reservation.Property<Guid>("RequestId").CurrentValue);
            Assert.False(reservation.Property<Guid>("RequestId").IsModified);
            var inventory = Assert.Single(context.ChangeTracker.Entries<InventoryEntity>());
            Assert.Equal(EntityState.Modified, inventory.State);
            Assert.Equal(50, inventory.Property(item => item.QuantityReserved).OriginalValue);
            Assert.Equal(20, inventory.Property(item => item.QuantityReserved).CurrentValue);
            Assert.True(inventory.Property(item => item.QuantityReserved).IsModified);
            Assert.False(inventory.Property(item => item.QuantityOnHand).IsModified);
            Assert.Equal(100, inventory.Entity.QuantityOnHand);
            Assert.Equal(7u, inventory.Property<uint>("Version").OriginalValue);
            Assert.Empty(context.ChangeTracker.Entries<StockMovement>());
            Assert.Empty(inventory.Entity.PendingMovements);
        });
        await using var context = CreateContext(interceptor);
        var (inventory, reservation) = Prepare(context, version, requestId);
        reservation.Release(Now, actorId);
        inventory.ReleaseReservedStock(30);

        await new StockReservationRepository(context).SaveReleaseAsync(
            reservation, inventory, cancellation.Token);

        Assert.Equal(1, interceptor.CallCount);
    }

    [Theory]
    [InlineData("concurrency")]
    [InlineData("unexpected")]
    [InlineData("database")]
    [InlineData("cancelled")]
    public async Task Release_WhenSaveFails_TranslatesOnlyConcurrencyAndRejectsReuse(string kind)
    {
        Exception failure = kind switch
        {
            "concurrency" => new DbUpdateConcurrencyException("Simulated stale data"),
            "database" => new DbUpdateException("Unrelated database failure"),
            "cancelled" => new OperationCanceledException("Simulated save cancellation"),
            _ => new InvalidOperationException("Simulated failure")
        };
        var interceptor = new InspectSave((_, _) => throw failure);
        await using var context = CreateContext(interceptor);
        var (inventory, reservation) = Prepare(context);
        reservation.Release(Now, Guid.NewGuid());
        inventory.ReleaseReservedStock(30);
        var repository = new StockReservationRepository(context);

        var error = await Record.ExceptionAsync(() => repository.SaveReleaseAsync(reservation, inventory));

        if (kind == "concurrency")
            Assert.Equal(reservation.Id, Assert.IsType<StockReservationConflictException>(error).ReservationId);
        else
            Assert.Same(failure, error);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveReleaseAsync(reservation, inventory));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.GetByIdAsync(reservation.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.GetByRequestIdAsync(Guid.NewGuid()));
        Assert.Equal(1, interceptor.CallCount);
        Assert.Equal(EntityState.Modified, context.Entry(reservation).State);
        Assert.Equal(StockReservationStatus.Active,
            context.Entry(reservation).Property(item => item.Status).OriginalValue);
        Assert.Equal(50, context.Entry(inventory).Property(item => item.QuantityReserved).OriginalValue);
        Assert.Equal(20, inventory.QuantityReserved);
        Assert.Empty(inventory.PendingMovements);
        // A failed save leaves mutated tracked objects; these tests do not simulate database rollback.
    }

    [Theory]
    [InlineData("reservationDetached")]
    [InlineData("reservationAdded")]
    [InlineData("reservationDeleted")]
    [InlineData("inventoryDetached")]
    [InlineData("inventoryAdded")]
    [InlineData("inventoryDeleted")]
    [InlineData("stillActive")]
    [InlineData("alreadyReleased")]
    [InlineData("fulfilled")]
    [InlineData("wrongInventory")]
    [InlineData("wrongDelta")]
    [InlineData("physicalChange")]
    [InlineData("pendingMovements")]
    public async Task Release_RejectsInvalidTransitionOrBalanceBeforeSaving(string invalid)
    {
        var interceptor = new InspectSave();
        await using var context = CreateContext(interceptor);
        var (inventory, reservation) = Prepare(context);
        if (invalid == "fulfilled") reservation.Fulfill(Now, Guid.NewGuid());
        else if (invalid != "stillActive") reservation.Release(Now, Guid.NewGuid());
        inventory.ReleaseReservedStock(invalid == "wrongDelta" ? 29 : 30);
        if (invalid == "alreadyReleased")
            context.Entry(reservation).Property(item => item.Status).OriginalValue = StockReservationStatus.Released;
        if (invalid == "wrongInventory")
        {
            inventory = new InventoryEntity(Guid.NewGuid(), Guid.NewGuid());
            inventory.ReceiveStock(100, "Other stock", Now);
            inventory.ReserveStock(50);
            inventory.AcknowledgePersistedMovements(inventory.PendingMovements.Select(item => item.Id).ToArray());
            context.Attach(inventory);
            inventory.ReleaseReservedStock(30);
        }
        if (invalid is "physicalChange" or "pendingMovements")
        {
            inventory.ReceiveStock(1, "Unexpected receipt", Now);
            if (invalid == "physicalChange")
                inventory.AcknowledgePersistedMovements(inventory.PendingMovements.Select(item => item.Id).ToArray());
            else
                inventory.RemoveStock(1, "Unexpected removal", Now);
        }
        if (invalid.StartsWith("reservation", StringComparison.Ordinal))
            context.Entry(reservation).State = invalid switch
            {
                "reservationDetached" => EntityState.Detached,
                "reservationAdded" => EntityState.Added,
                _ => EntityState.Deleted
            };
        // Defer relationship processing so the repository can reject the deleted principal.
        if (invalid == "inventoryDeleted")
            context.ChangeTracker.CascadeDeleteTiming =
                Microsoft.EntityFrameworkCore.ChangeTracking.CascadeTiming.Never;
        if (invalid.StartsWith("inventory", StringComparison.Ordinal))
            context.Entry(inventory).State = invalid switch
            {
                "inventoryDetached" => EntityState.Detached,
                "inventoryAdded" => EntityState.Added,
                _ => EntityState.Deleted
            };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new StockReservationRepository(context).SaveReleaseAsync(reservation, inventory));
        Assert.Equal(0, interceptor.CallCount);
    }

    [Fact]
    public async Task Release_WithAmbientTransaction_RejectsBeforeSaving()
    {
        var interceptor = new InspectSave();
        await using var context = CreateContext(interceptor);
        var (inventory, reservation) = Prepare(context);
        reservation.Release(Now, Guid.NewGuid());
        inventory.ReleaseReservedStock(30);
        using var transaction = new System.Transactions.TransactionScope(
            System.Transactions.TransactionScopeAsyncFlowOption.Enabled);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new StockReservationRepository(context).SaveReleaseAsync(reservation, inventory));
        Assert.Equal(0, interceptor.CallCount);
    }

    [Fact]
    public async Task Release_WhenAlreadyCancelled_DoesNotSave()
    {
        var interceptor = new InspectSave();
        await using var context = CreateContext(interceptor);
        var (inventory, reservation) = Prepare(context);
        reservation.Release(Now, Guid.NewGuid());
        inventory.ReleaseReservedStock(30);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new StockReservationRepository(context).SaveReleaseAsync(reservation, inventory, cancellation.Token));
        Assert.Equal(0, interceptor.CallCount);
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
        var reservationVersion = version ?? Guid.NewGuid();
        entry.Property<Guid>("Version").CurrentValue = reservationVersion;
        entry.Property<Guid>("Version").OriginalValue = reservationVersion;
        entry.Property<Guid>("Version").IsModified = false;
        var creationRequestId = requestId ?? Guid.NewGuid();
        entry.Property<Guid>("RequestId").CurrentValue = creationRequestId;
        entry.Property<Guid>("RequestId").OriginalValue = creationRequestId;
        entry.Property<Guid>("RequestId").IsModified = false;
        return (inventory, reservation);
    }

    private static StockFlowDbContext CreateContext(SaveChangesInterceptor interceptor) =>
        new(new DbContextOptionsBuilder<StockFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=unused_reservation_release_test")
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
            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(2));
        }
    }
}
