using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Application.StockReservations.ReleaseStockReservation;
using StockFlow.Application.Tests.Fakes;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Tests;

public sealed class ReleaseStockReservationCommandHandlerTests
{
    [Theory]
    [InlineData(30)]
    [InlineData(50)]
    public async Task Release_RecordsActorAndFreesOnlyThisReservation(int reserved)
    {
        var f = new Fixture(reserved: reserved);
        using var cancellation = new CancellationTokenSource();
        var originalId = f.Reservation.Id;
        var originalCreator = f.Reservation.CreatedByUserId;

        await f.Handler.HandleAsync(f.Command, cancellation.Token);

        Assert.Equal(StockReservationStatus.Released, f.Reservation.Status);
        Assert.Equal(f.Clock.UtcNow, f.Reservation.ReleasedAt);
        Assert.Equal(f.User.UserId, f.Reservation.ReleasedByUserId);
        Assert.Null(f.Reservation.FulfilledAt);
        Assert.Null(f.Reservation.FulfilledByUserId);
        Assert.Equal(originalId, f.Reservation.Id);
        Assert.Equal(originalCreator, f.Reservation.CreatedByUserId);
        Assert.Equal(30, f.Reservation.Quantity);
        Assert.Equal(100, f.Inventory.QuantityOnHand);
        Assert.Equal(reserved - 30, f.Inventory.QuantityReserved);
        Assert.Equal(130 - reserved, f.Inventory.QuantityAvailable);
        Assert.Empty(f.Inventory.PendingMovements);
        Assert.Same(f.Reservation, f.Reservations.LastSavedReservation);
        Assert.Same(f.Inventory, f.Reservations.LastSavedInventory);
        Assert.Equal(f.Reservation.Id, f.Reservations.ReceivedReservationId);
        Assert.Equal(cancellation.Token, f.Reservations.ReceivedToken);
        Assert.Single(f.Notifier.Notifications);
        Assert.Equal(cancellation.Token, f.Notifier.LastCancellationToken);
        Assert.Equal(1, f.Reservations.ReleaseSaveCallCount);
        Assert.Equal(0, f.Reservations.SaveCallCount);
        Assert.Equal(0, f.Inventories.SaveCallCount);
    }

    [Fact]
    public async Task Release_WithNullCommand_DoesNotRead()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ArgumentNullException>(() => f.Handler.HandleAsync(null!));
        f.AssertNoReads();
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("emptyUser")]
    [InlineData("wrongPermission")]
    public async Task Release_WithoutAccess_DoesNotRead(string scenario)
    {
        var f = new Fixture();
        if (scenario == "anonymous") f.User.UserId = null;
        if (scenario == "emptyUser") f.User.UserId = Guid.Empty;
        if (scenario == "wrongPermission")
        {
            f.User.Permissions.Clear();
            f.User.Permissions.Add(StockFlowPolicies.StockReservationsFulfill);
        }
        if (scenario == "wrongPermission")
            await Assert.ThrowsAsync<PermissionDeniedException>(() => f.Handler.HandleAsync(f.Command));
        else
            await Assert.ThrowsAsync<AuthenticationRequiredException>(() => f.Handler.HandleAsync(f.Command));
        f.AssertNoReads();
    }

    [Theory]
    [InlineData(true, "ReservationId")]
    [InlineData(false, "ExpectedVersion")]
    public async Task Release_WithEmptyIdentifier_DoesNotRead(bool reservation, string field)
    {
        var f = new Fixture();
        var command = reservation
            ? f.Command with { ReservationId = Guid.Empty }
            : f.Command with { ExpectedVersion = Guid.Empty };
        var error = await Assert.ThrowsAsync<RequestValidationException>(() => f.Handler.HandleAsync(command));
        Assert.Equal(field, error.FieldName);
        f.AssertNoReads();
    }

    [Theory]
    [InlineData("Stock reservation")]
    [InlineData("Inventory")]
    public async Task Release_WithMissingResource_DoesNotMutate(string resource)
    {
        var f = new Fixture(missing: resource);
        var error = await Assert.ThrowsAsync<ResourceNotFoundException>(() => f.Handler.HandleAsync(f.Command));
        Assert.Equal(resource, error.ResourceName);
        Assert.Equal(resource == "Inventory" ? f.Inventory.Id : f.Reservation.Id, error.ResourceId);
        f.AssertUnchanged();
    }

    [Fact]
    public async Task Release_WithStaleVersion_DoesNotLoadInventory()
    {
        var f = new Fixture();
        var error = await Assert.ThrowsAsync<StockReservationConflictException>(() =>
            f.Handler.HandleAsync(f.Command with { ExpectedVersion = Guid.NewGuid() }));
        Assert.Equal(f.Reservation.Id, error.ReservationId);
        Assert.Equal(0, f.Inventories.GetCallCount);
        f.AssertUnchanged();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Release_WithTerminalReservation_ConflictsWithoutInventoryLookup(bool released)
    {
        var f = new Fixture();
        var actor = Guid.NewGuid();
        if (released) f.Reservation.Release(f.Clock.UtcNow, actor);
        else f.Reservation.Fulfill(f.Clock.UtcNow, actor);
        var status = f.Reservation.Status;
        var releasedAt = f.Reservation.ReleasedAt;
        var fulfilledAt = f.Reservation.FulfilledAt;

        await Assert.ThrowsAsync<StockReservationConflictException>(() => f.Handler.HandleAsync(f.Command));

        Assert.Equal(status, f.Reservation.Status);
        Assert.Empty(f.Notifier.Notifications);
        Assert.Equal(releasedAt, f.Reservation.ReleasedAt);
        Assert.Equal(fulfilledAt, f.Reservation.FulfilledAt);
        Assert.Equal(0, f.Inventories.GetCallCount);
        Assert.Equal(0, f.Reservations.ReleaseSaveCallCount);
        Assert.Equal(50, f.Inventory.QuantityReserved);
        Assert.Empty(f.Inventory.PendingMovements);
    }

    [Fact]
    public async Task Release_WithInconsistentReservedBalance_DoesNotMutateReservation()
    {
        var f = new Fixture(reserved: 20);
        await Assert.ThrowsAsync<StockReservationConflictException>(() => f.Handler.HandleAsync(f.Command));
        f.AssertUnchanged();
    }

    [Theory]
    [InlineData("default")]
    [InlineData("nonUtc")]
    [InlineData("beforeCreation")]
    public async Task Release_WithInvalidClock_PreservesBothAggregates(string invalid)
    {
        var f = new Fixture();
        f.Clock.UtcNow = invalid switch
        {
            "default" => default,
            "nonUtc" => f.Clock.UtcNow.ToOffset(TimeSpan.FromHours(2)),
            _ => f.Reservation.CreatedAt.AddTicks(-1)
        };
        await Assert.ThrowsAnyAsync<ArgumentException>(() => f.Handler.HandleAsync(f.Command));
        f.AssertUnchanged();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Release_WhenSaveFails_PropagatesWithoutSeparateSavesOrRetry(bool conflict)
    {
        var f = new Fixture();
        Exception failure = conflict
            ? new StockReservationConflictException(f.Reservation.Id, "Simulated concurrency conflict")
            : new InvalidOperationException("Simulated save failure");
        f.Reservations.SaveException = failure;

        Assert.Same(failure, await Record.ExceptionAsync(() => f.Handler.HandleAsync(f.Command)));
        Assert.Equal(1, f.Reservations.ReleaseSaveCallCount);
        Assert.Equal(0, f.Inventories.SaveCallCount);
        Assert.Equal(0, f.Reservations.SaveCallCount);
        Assert.Null(f.Reservations.LastSavedReservation);
        Assert.Null(f.Reservations.LastSavedInventory);
        Assert.Empty(f.Notifier.Notifications);
        // Fakes do not roll back mutated objects; discard this operation's scope.
        Assert.Equal(StockReservationStatus.Released, f.Reservation.Status);
        Assert.Equal(20, f.Inventory.QuantityReserved);
        Assert.Equal(100, f.Inventory.QuantityOnHand);
        Assert.Empty(f.Inventory.PendingMovements);
    }

    [Fact]
    public async Task Release_WhenAlreadyCancelled_DoesNotRead()
    {
        var f = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            f.Handler.HandleAsync(f.Command, cancellation.Token));
        f.AssertNoReads();
    }

    [Fact]
    public async Task Release_WhenRepeated_DoesNotDecreaseReservedBalanceTwice()
    {
        var f = new Fixture();
        await f.Handler.HandleAsync(f.Command);
        await Assert.ThrowsAsync<StockReservationConflictException>(() => f.Handler.HandleAsync(f.Command));
        Assert.Equal(1, f.Reservations.ReleaseSaveCallCount);
        Assert.Equal(20, f.Inventory.QuantityReserved);
        Assert.Single(f.Notifier.Notifications);
        Assert.Equal(100, f.Inventory.QuantityOnHand);
        Assert.Empty(f.Inventory.PendingMovements);
    }

    private sealed class Fixture
    {
        public FakeCurrentUser User { get; } = new() { UserId = Guid.NewGuid() };
        public FakeClock Clock { get; } = new();
        public FakeInventoryChangeNotifier Notifier { get; } = new();
        public FakeStockReservationRepository Reservations { get; } = new();
        public FakeInventoryRepository Inventories { get; } = new();
        public InventoryEntity Inventory { get; }
        public StockReservation Reservation { get; }
        public ReleaseStockReservationCommand Command { get; }
        public ReleaseStockReservationCommandHandler Handler { get; }
        private readonly int _originalReserved;

        public Fixture(int reserved = 50, string? missing = null)
        {
            _originalReserved = reserved;
            User.Permissions.Add(StockFlowPolicies.StockReservationsManage);
            Inventory = new InventoryEntity(Guid.NewGuid(), Guid.NewGuid());
            Inventory.ReceiveStock(100, "Synthetic stock", Clock.UtcNow.AddHours(-1));
            Inventory.ReserveStock(reserved);
            Inventory.AcknowledgePersistedMovements(Inventory.PendingMovements.Select(item => item.Id).ToArray());
            Reservation = new StockReservation(Inventory.Id, 30, "Operation", Clock.UtcNow.AddHours(-1), Guid.NewGuid());
            var version = Guid.NewGuid();
            if (missing != "Stock reservation") Reservations.AddTracked(Reservation, version);
            if (missing != "Inventory") Inventories.Add(Inventory);
            Command = new ReleaseStockReservationCommand(Reservation.Id, version);
            Handler = new ReleaseStockReservationCommandHandler(User, Reservations, Inventories, Clock, Notifier);
            Notifier.OnNotify = _ =>
            {
                Assert.Equal(1, Reservations.ReleaseSaveCallCount);
                Assert.Same(Reservation, Reservations.LastSavedReservation);
                Assert.Same(Inventory, Reservations.LastSavedInventory);
            };
        }

        public void AssertUnchanged()
        {
            Assert.Empty(Notifier.Notifications);
            Assert.Equal(StockReservationStatus.Active, Reservation.Status);
            Assert.Null(Reservation.ReleasedAt);
            Assert.Null(Reservation.ReleasedByUserId);
            Assert.Null(Reservation.FulfilledAt);
            Assert.Null(Reservation.FulfilledByUserId);
            Assert.Equal(_originalReserved, Inventory.QuantityReserved);
            Assert.Equal(100, Inventory.QuantityOnHand);
            Assert.Empty(Inventory.PendingMovements);
            Assert.Equal(0, Reservations.ReleaseSaveCallCount);
            Assert.Equal(0, Reservations.SaveCallCount);
            Assert.Equal(0, Inventories.SaveCallCount);
        }

        public void AssertNoReads()
        {
            Assert.Equal(0, Reservations.LookupByIdCallCount);
            Assert.Equal(0, Inventories.GetCallCount);
            AssertUnchanged();
        }
    }
}
