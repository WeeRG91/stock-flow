using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using StockFlow.Infrastructure.Persistence;
using StockFlow.Infrastructure.Persistence.Repositories;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Tests;

public sealed class TransferReceiptRepositoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveConfirmationAsync_StagesReceiptAndBalanceAndAcknowledgesAfterSave(
        bool existingBalance)
    {
        using var fixture = new Fixture(existingBalance);
        var interceptor = fixture.Interceptor;
        interceptor.Inspect = fixture.AssertStaged;

        await fixture.SaveAsync();

        Assert.Equal(1, interceptor.CallCount);
        Assert.Empty(fixture.Inventory.PendingMovements);
        Assert.Empty(fixture.Transfer.PendingReceipts);
        Assert.Equal(TransferStatus.PartiallyReceived, fixture.Transfer.Status);
        Assert.Equal(2, Assert.Single(fixture.Transfer.Lines).ReceivedQuantity);

        // Suppressed saves bypass EF's normal state acceptance.
        fixture.Context.ChangeTracker.AcceptAllChanges();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.SaveAsync());
        Assert.Equal(1, interceptor.CallCount);
    }

    [Theory]
    [InlineData("concurrency")]
    [InlineData("receipt-duplicate")]
    [InlineData("balance-duplicate")]
    [InlineData("unrelated-unique")]
    [InlineData("check")]
    [InlineData("unexpected")]
    public async Task SaveConfirmationAsync_FailurePreservesPendingRecordsAndPreventsReuse(
        string scenario)
    {
        using var fixture = new Fixture(existingBalance: true);
        var failure = CreateFailure(scenario);
        fixture.Interceptor.Inspect = context =>
        {
            fixture.AssertStaged(context);
            throw failure;
        };

        var error = await Record.ExceptionAsync(() => fixture.SaveAsync());

        if (scenario is "concurrency" or "receipt-duplicate" or "balance-duplicate")
        {
            var conflict = Assert.IsType<TransferConflictException>(error);
            Assert.Equal(fixture.Transfer.Id, conflict.TransferId);
        }
        else
        {
            Assert.Same(failure, error);
        }

        Assert.Same(fixture.Receipt, Assert.Single(fixture.Transfer.PendingReceipts));
        Assert.Same(fixture.Movement, Assert.Single(fixture.Inventory.PendingMovements));
        Assert.Equal(1, fixture.Interceptor.CallCount);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.SaveAsync());
        // GetByIdAsync rejects synchronously before it could execute a query.
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = fixture.Repository.GetByIdAsync(fixture.Receipt.Id);
        });
        Assert.Equal(1, fixture.Interceptor.CallCount);
    }

    [Theory]
    [InlineData("missing-balance")]
    [InlineData("wrong-warehouse")]
    [InlineData("unapplied-receipt")]
    public async Task SaveConfirmationAsync_InvalidParticipants_DoNotReachSave(string scenario)
    {
        using var fixture = new Fixture(existingBalance: true);
        IReadOnlyList<InventoryEntity> inventories = [fixture.Inventory];
        var receipt = fixture.Receipt;
        if (scenario == "missing-balance") inventories = [];
        if (scenario == "wrong-warehouse")
        {
            inventories = [new InventoryEntity(fixture.Inventory.ProductId, Guid.NewGuid())];
        }
        if (scenario == "unapplied-receipt")
        {
            receipt = new TransferReceipt(Guid.NewGuid(), fixture.Transfer.Id,
                fixture.Receipt.ConfirmedAt, fixture.Receipt.ConfirmedByUserId,
                fixture.Receipt.Lines);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Repository.SaveConfirmationAsync(receipt, fixture.Transfer, inventories));

        Assert.Equal(0, fixture.Interceptor.CallCount);
        Assert.Same(fixture.Receipt, Assert.Single(fixture.Transfer.PendingReceipts));
        Assert.Same(fixture.Movement, Assert.Single(fixture.Inventory.PendingMovements));
        Assert.Empty(fixture.Context.ChangeTracker.Entries<TransferReceipt>());
        Assert.Empty(fixture.Context.ChangeTracker.Entries<StockMovement>());
    }

    private static Exception CreateFailure(string scenario)
    {
        if (scenario == "concurrency")
            return new DbUpdateConcurrencyException("Simulated stale transfer or inventory.");
        if (scenario == "unexpected")
            return new InvalidOperationException("Simulated unexpected save failure.");

        var constraint = scenario switch
        {
            "receipt-duplicate" => "PK_TransferReceipts",
            "balance-duplicate" => "IX_Inventories_ProductId_WarehouseId",
            "check" => "CK_TransferReceiptLines_ReceivedQuantity_Positive",
            _ => "SomeOtherUniqueConstraint"
        };
        var postgresFailure = new PostgresException(
            messageText: "Simulated database failure.",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: scenario == "check"
                ? PostgresErrorCodes.CheckViolation : PostgresErrorCodes.UniqueViolation,
            constraintName: constraint);
        return new DbUpdateException("Simulated persistence failure.", postgresFailure);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly bool _existingBalance;
        private readonly Guid _originalVersion = Guid.NewGuid();
        public InspectSaveInterceptor Interceptor { get; } = new();
        public StockFlowDbContext Context { get; }
        public TransferReceiptRepository Repository { get; }
        public Transfer Transfer { get; }
        public TransferReceipt Receipt { get; }
        public InventoryEntity Inventory { get; }
        public StockMovement Movement { get; }

        public Fixture(bool existingBalance)
        {
            _existingBalance = existingBalance;
            var now = new DateTimeOffset(2026, 11, 23, 11, 0, 0, TimeSpan.Zero);
            Transfer = new Transfer(Guid.NewGuid(), "BRU", "Brussels",
                Guid.NewGuid(), "LIE", "Liège", now.AddHours(-1), Guid.NewGuid());
            Transfer.AddLine(Guid.NewGuid(), "CHAIR", "Chair", 5);
            Transfer.Dispatch(now.AddMinutes(-2), Guid.NewGuid());
            var line = Assert.Single(Transfer.Lines);
            Inventory = new InventoryEntity(line.ProductId, Transfer.DestinationWarehouseId);
            if (existingBalance)
            {
                Inventory.ReceiveStock(7, "Opening stock", now.AddHours(-1));
                Inventory.AcknowledgePersistedMovements(
                    Inventory.PendingMovements.Select(movement => movement.Id).ToArray());
            }

            var options = new DbContextOptionsBuilder<StockFlowDbContext>()
                .UseNpgsql("Host=localhost;Database=unused_transfer_receipt_test")
                .AddInterceptors(Interceptor)
                .Options;
            Context = new StockFlowDbContext(options);
            var version = Context.Transfers.Attach(Transfer).Property<Guid>("Version");
            version.CurrentValue = _originalVersion;
            version.OriginalValue = _originalVersion;
            version.IsModified = false;
            if (existingBalance)
            {
                var inventoryVersion = Context.Inventories.Attach(Inventory).Property<uint>("Version");
                inventoryVersion.CurrentValue = 7u;
                inventoryVersion.OriginalValue = 7u;
                inventoryVersion.IsModified = false;
            }

            Receipt = new TransferReceipt(Guid.NewGuid(), Transfer.Id, now, Guid.NewGuid(),
                [new TransferReceiptLine(line.Id, 2)]);
            Transfer.ApplyReceipt(Receipt);
            Inventory.ReceiveStock(2, $"Transfer {Transfer.Id} receipt {Receipt.Id}", now);
            Movement = Assert.Single(Inventory.PendingMovements);
            Repository = new TransferReceiptRepository(Context);
        }

        public Task SaveAsync() =>
            Repository.SaveConfirmationAsync(Receipt, Transfer, [Inventory]);

        public void AssertStaged(StockFlowDbContext context)
        {
            var transferEntry = context.Entry(Transfer);
            Assert.Equal(EntityState.Modified, transferEntry.State);
            var version = transferEntry.Property<Guid>("Version");
            Assert.Equal(_originalVersion, version.OriginalValue);
            Assert.NotEqual(_originalVersion, version.CurrentValue);
            Assert.NotEqual(Guid.Empty, version.CurrentValue);
            Assert.True(version.IsModified);
            var transferLine = Assert.Single(context.ChangeTracker.Entries<TransferLine>());
            Assert.Equal(EntityState.Modified, transferLine.State);
            Assert.Equal(0, transferLine.Property(line => line.ReceivedQuantity).OriginalValue);
            Assert.Equal(2, transferLine.Entity.ReceivedQuantity);

            var receiptEntry = Assert.Single(context.ChangeTracker.Entries<TransferReceipt>());
            Assert.Same(Receipt, receiptEntry.Entity);
            Assert.Equal(EntityState.Added, receiptEntry.State);
            var receiptLine = Assert.Single(context.ChangeTracker.Entries<TransferReceiptLine>());
            Assert.Equal(EntityState.Added, receiptLine.State);
            Assert.Equal(Receipt.Id, receiptLine.Property<Guid>("TransferReceiptId").CurrentValue);
            Assert.Equal(Transfer.Lines[0].Id, receiptLine.Entity.TransferLineId);

            var balance = Assert.Single(context.ChangeTracker.Entries<InventoryEntity>());
            Assert.Same(Inventory, balance.Entity);
            Assert.Equal(_existingBalance ? EntityState.Modified : EntityState.Added, balance.State);
            Assert.Equal(_existingBalance ? 9 : 2, Inventory.QuantityOnHand);
            if (_existingBalance)
            {
                Assert.Equal(7, balance.Property(item => item.QuantityOnHand).OriginalValue);
                Assert.Equal(7u, balance.Property<uint>("Version").OriginalValue);
            }
            var movement = Assert.Single(context.ChangeTracker.Entries<StockMovement>());
            Assert.Same(Movement, movement.Entity);
            Assert.Equal(EntityState.Added, movement.State);
            Assert.Equal(2, movement.Entity.QuantityChange);
            Assert.Equal(StockMovementType.Receipt, movement.Entity.Type);
            Assert.Equal(Inventory.Id, movement.Entity.InventoryId);
            Assert.Same(Receipt, Assert.Single(Transfer.PendingReceipts));
            Assert.Same(Movement, Assert.Single(Inventory.PendingMovements));
        }

        public void Dispose() => Context.Dispose();
    }

    private sealed class InspectSaveInterceptor : SaveChangesInterceptor
    {
        public int CallCount { get; private set; }
        public Action<StockFlowDbContext>? Inspect { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Inspect?.Invoke(Assert.IsType<StockFlowDbContext>(eventData.Context));
            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(5));
        }
    }
}
