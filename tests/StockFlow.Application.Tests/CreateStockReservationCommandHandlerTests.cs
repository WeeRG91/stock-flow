using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Application.StockReservations.CreateStockReservation;
using StockFlow.Application.Tests.Fakes;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Tests;

public sealed class CreateStockReservationCommandHandlerTests
{
    [Fact]
    public async Task Create_WhenRetriedAfterSuccess_DoesNotAllocateTwice()
    {
        var f = new Fixture();

        var first = await f.Handler.HandleAsync(f.Command);
        var replay = await f.Handler.HandleAsync(f.Command);

        Assert.Equal(first, replay);
        Assert.Single(f.Notifier.Notifications);
        Assert.Equal(1, f.Reservations.SaveCallCount);
        Assert.Equal(1, f.Inventories.GetCallCount);
        Assert.Equal(60, f.Inventory.QuantityReserved);
        Assert.Equal(40, f.Inventory.QuantityAvailable);
        Assert.Equal(100, f.Inventory.QuantityOnHand);
        Assert.Single(f.Inventory.PendingMovements);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(70)]
    public async Task Create_AllocatesAvailableStockAndSavesBothEntities(int quantity)
    {
        var f = new Fixture();
        using var cancellation = new CancellationTokenSource();
        var command = f.Command with { Quantity = quantity, Reference = "  Operation A  " };
        var movements = f.Inventory.PendingMovements.ToArray();

        var result = await f.Handler.HandleAsync(command, cancellation.Token);

        var saved = f.Reservations.LastSavedReservation;
        Assert.NotNull(saved);
        Assert.NotEqual(Guid.Empty, result.ReservationId);
        Assert.Equal(saved.Id, result.ReservationId);
        Assert.Equal(f.Inventory.Id, result.InventoryId);
        Assert.Equal(quantity, result.Quantity);
        Assert.Equal("Operation A", result.Reference);
        Assert.Equal(StockReservationStatus.Active, result.Status);
        Assert.Equal(f.Clock.UtcNow, result.CreatedAt);
        Assert.Equal(f.User.UserId, result.CreatedByUserId);
        Assert.Same(f.Inventory, f.Reservations.LastSavedInventory);
        Assert.Equal(100, f.Inventory.QuantityOnHand);
        Assert.Equal(30 + quantity, f.Inventory.QuantityReserved);
        Assert.Equal(70 - quantity, f.Inventory.QuantityAvailable);
        Assert.Equal(movements, f.Inventory.PendingMovements.ToArray());
        Assert.Equal(1, f.Reservations.SaveCallCount);
        Assert.Equal(command.RequestId, f.Reservations.ReceivedRequestId);
        Assert.Equal(cancellation.Token, f.Reservations.ReceivedToken);
        Assert.Equal(0, f.Inventories.SaveCallCount);
        Assert.Single(f.Notifier.Notifications);
        Assert.Equal(cancellation.Token, f.Notifier.LastCancellationToken);
    }

    [Theory]
    [InlineData("request", "RequestId")]
    [InlineData("inventory", "InventoryId")]
    [InlineData("zero", "Quantity")]
    [InlineData("negative", "Quantity")]
    [InlineData("minimum", "Quantity")]
    [InlineData("null", "Reference")]
    [InlineData("empty", "Reference")]
    [InlineData("blank", "Reference")]
    public async Task Create_WithInvalidCommand_DoesNotAccessRepositories(string input, string field)
    {
        var f = new Fixture();
        var command = input switch
        {
            "request" => f.Command with { RequestId = Guid.Empty },
            "inventory" => f.Command with { InventoryId = Guid.Empty },
            "zero" => f.Command with { Quantity = 0 },
            "negative" => f.Command with { Quantity = -1 },
            "minimum" => f.Command with { Quantity = int.MinValue },
            "null" => f.Command with { Reference = null! },
            "empty" => f.Command with { Reference = "" },
            _ => f.Command with { Reference = " \t\r\n" }
        };

        var error = await Assert.ThrowsAsync<RequestValidationException>(
            () => f.Handler.HandleAsync(command));

        Assert.Equal(field, error.FieldName);
        f.AssertNoReadsOrWrites();
    }

    [Fact]
    public async Task Create_WithNullCommand_RejectsWithoutReading()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ArgumentNullException>(() => f.Handler.HandleAsync(null!));
        f.AssertNoReadsOrWrites();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Create_WithoutValidUser_RejectsBeforeReading(bool nullUser)
    {
        var f = new Fixture();
        f.User.UserId = nullUser ? null : Guid.Empty;
        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => f.Handler.HandleAsync(f.Command));
        f.AssertNoReadsOrWrites();
    }

    [Fact]
    public async Task Create_WithoutPermission_RejectsBeforeReading()
    {
        var f = new Fixture();
        f.User.Permissions.Clear();
        f.User.Permissions.Add(StockFlowPolicies.InventoryReceive);
        await Assert.ThrowsAsync<PermissionDeniedException>(
            () => f.Handler.HandleAsync(f.Command));
        f.AssertNoReadsOrWrites();
    }

    [Fact]
    public async Task Create_WhenAlreadyCancelled_DoesNotRead()
    {
        var f = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => f.Handler.HandleAsync(f.Command, cancellation.Token));
        f.AssertNoReadsOrWrites();
    }

    [Theory]
    [InlineData("Inventory")]
    [InlineData("Product")]
    [InlineData("Warehouse")]
    public async Task Create_WithMissingResource_DoesNotAllocate(string resource)
    {
        var f = new Fixture(resource);
        var error = await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => f.Handler.HandleAsync(f.Command));
        Assert.Equal(resource, error.ResourceName);
        Assert.Equal(resource switch
        {
            "Inventory" => f.Inventory.Id,
            "Product" => f.Product.Id,
            _ => f.Warehouse.Id
        }, error.ResourceId);
        f.AssertUnchanged();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Create_WithInactiveResource_DoesNotAllocate(bool product)
    {
        var f = new Fixture();
        if (product) f.Product.Deactivate();
        else f.Warehouse.Deactivate();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Handler.HandleAsync(f.Command));
        f.AssertUnchanged();
    }

    [Fact]
    public async Task Create_WithInsufficientAvailableStock_DoesNotAllocate()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Handler.HandleAsync(f.Command with { Quantity = 71 }));
        f.AssertUnchanged();
    }

    [Theory]
    [InlineData(StockReservationStatus.Active)]
    [InlineData(StockReservationStatus.Released)]
    [InlineData(StockReservationStatus.Fulfilled)]
    public async Task Create_WithExactReplay_ReturnsCurrentStatusWithoutAllocation(
        StockReservationStatus status)
    {
        var f = new Fixture();
        var existing = new StockReservation(f.Inventory.Id, 30, "Operation A", f.Clock.UtcNow, f.User.UserId!.Value);
        if (status == StockReservationStatus.Released)
            existing.Release(f.Clock.UtcNow, f.User.UserId.Value);
        if (status == StockReservationStatus.Fulfilled)
            existing.Fulfill(f.Clock.UtcNow, f.User.UserId.Value);
        f.Reservations.Add(f.Command.RequestId, existing);
        f.Product.Deactivate();
        f.Warehouse.Deactivate();
        using var cancellation = new CancellationTokenSource();

        var result = await f.Handler.HandleAsync(
            f.Command with { Reference = " Operation A " }, cancellation.Token);

        Assert.Equal(existing.Id, result.ReservationId);
        Assert.Equal(status, result.Status);
        Assert.Equal(existing.CreatedAt, result.CreatedAt);
        Assert.Equal(existing.CreatedByUserId, result.CreatedByUserId);
        Assert.Equal(cancellation.Token, f.Reservations.ReceivedToken);
        Assert.Equal(1, f.Reservations.GetCallCount);
        Assert.Equal(0, f.Inventories.GetCallCount);
        Assert.Equal(0, f.Products.GetByIdCallCount);
        Assert.Equal(0, f.Warehouses.GetByIdCallCount);
        f.AssertUnchanged();
    }

    [Theory]
    [InlineData("creator")]
    [InlineData("inventory")]
    [InlineData("quantity")]
    [InlineData("reference")]
    public async Task Create_WithMismatchedReplay_ConflictsBeforeInventoryLookup(string mismatch)
    {
        var f = new Fixture();
        var existing = new StockReservation(
            mismatch == "inventory" ? Guid.NewGuid() : f.Inventory.Id,
            mismatch == "quantity" ? 31 : 30,
            mismatch == "reference" ? "operation a" : "Operation A",
            f.Clock.UtcNow,
            mismatch == "creator" ? Guid.NewGuid() : f.User.UserId!.Value);
        f.Reservations.Add(f.Command.RequestId, existing);

        var error = await Assert.ThrowsAsync<StockReservationConflictException>(
            () => f.Handler.HandleAsync(f.Command));

        Assert.Equal(existing.Id, error.ReservationId);
        Assert.Equal(0, f.Inventories.GetCallCount);
        f.AssertUnchanged();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Create_WhenSaveFails_PropagatesWithoutSeparateInventorySave(bool conflict)
    {
        var f = new Fixture();
        Exception failure = conflict
            ? new StockReservationConflictException(Guid.NewGuid(), "Concurrent update")
            : new InvalidOperationException("Simulated save failure");
        f.Reservations.SaveException = failure;

        var error = await Record.ExceptionAsync(() => f.Handler.HandleAsync(f.Command));

        Assert.Same(failure, error);
        Assert.Equal(1, f.Reservations.SaveCallCount);
        Assert.Null(f.Reservations.LastSavedReservation);
        Assert.Null(f.Reservations.LastSavedInventory);
        Assert.Empty(f.Notifier.Notifications);
        Assert.Equal(0, f.Inventories.SaveCallCount);
        // Fakes do not roll back mutated objects. This operation's scope must be discarded.
        Assert.Equal(60, f.Inventory.QuantityReserved);
        Assert.Equal(100, f.Inventory.QuantityOnHand);
        Assert.Single(f.Inventory.PendingMovements);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Create_WithInvalidClock_DoesNotAllocate(bool defaultTime)
    {
        var f = new Fixture();
        f.Clock.UtcNow = defaultTime ? default : f.Clock.UtcNow.ToOffset(TimeSpan.FromHours(2));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Handler.HandleAsync(f.Command));
        f.AssertUnchanged();
    }

    private sealed class Fixture
    {
        public FakeCurrentUser User { get; } = new() { UserId = Guid.NewGuid() };
        public FakeClock Clock { get; } = new();
        public FakeInventoryChangeNotifier Notifier { get; } = new();
        public FakeInventoryRepository Inventories { get; } = new();
        public FakeProductRepository Products { get; } = new();
        public FakeWarehouseRepository Warehouses { get; } = new();
        public FakeStockReservationRepository Reservations { get; } = new();
        public Product Product { get; } = new("Synthetic product", "TEST-RESERVATION");
        public Warehouse Warehouse { get; } = new("TEST-BRU", "Synthetic warehouse", "Brussels");
        public InventoryEntity Inventory { get; }
        public CreateStockReservationCommand Command { get; }
        public CreateStockReservationCommandHandler Handler { get; }
        private readonly StockMovement _originalMovement;

        public Fixture(string? missing = null)
        {
            User.Permissions.Add(StockFlowPolicies.StockReservationsManage);
            Inventory = new InventoryEntity(Product.Id, Warehouse.Id);
            Inventory.ReceiveStock(100, "Initial stock", Clock.UtcNow);
            Inventory.ReserveStock(30);
            _originalMovement = Assert.Single(Inventory.PendingMovements);
            if (missing != "Inventory") Inventories.Add(Inventory);
            if (missing != "Product") Products.Add(Product);
            if (missing != "Warehouse") Warehouses.Add(Warehouse);
            Command = new CreateStockReservationCommand(Guid.NewGuid(), Inventory.Id, 30, "Operation A");
            Handler = new CreateStockReservationCommandHandler(User, Inventories, Products, Warehouses, Reservations, Clock, Notifier);
            Notifier.OnNotify = _ =>
            {
                Assert.Equal(1, Reservations.SaveCallCount);
                Assert.NotNull(Reservations.LastSavedReservation);
                Assert.Same(Inventory, Reservations.LastSavedInventory);
            };
        }

        public void AssertUnchanged()
        {
            Assert.Empty(Notifier.Notifications);
            Assert.Equal(100, Inventory.QuantityOnHand);
            Assert.Equal(30, Inventory.QuantityReserved);
            Assert.Equal(70, Inventory.QuantityAvailable);
            Assert.Same(_originalMovement, Assert.Single(Inventory.PendingMovements));
            Assert.Equal(0, Reservations.SaveCallCount);
            Assert.Equal(0, Inventories.SaveCallCount);
        }

        public void AssertNoReadsOrWrites()
        {
            Assert.Equal(0, Reservations.GetCallCount);
            Assert.Equal(0, Inventories.GetCallCount);
            Assert.Equal(0, Products.GetByIdCallCount);
            Assert.Equal(0, Warehouses.GetByIdCallCount);
            AssertUnchanged();
        }
    }
}
