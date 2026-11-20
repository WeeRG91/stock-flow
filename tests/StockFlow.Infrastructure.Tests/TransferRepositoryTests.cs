using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using StockFlow.Infrastructure.Persistence;
using StockFlow.Infrastructure.Persistence.Repositories;

namespace StockFlow.Infrastructure.Tests;

public sealed class TransferRepositoryTests
{
    [Fact]
    public async Task SaveDraftAsync_StagesHeaderAndLinesInOneSave()
    {
        var transfer = new Transfer(
            Guid.NewGuid(), "BRU", "Brussels",
            Guid.NewGuid(), "LIE", "Liège",
            new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.Zero),
            Guid.NewGuid());

        transfer.AddLine(Guid.NewGuid(), "CHAIR", "Office chair", 4);
        transfer.AddLine(Guid.NewGuid(), "DESK", "Office desk", 2);

        var interceptor = new InspectDraftSaveInterceptor(transfer);
        var options = new DbContextOptionsBuilder<StockFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=unused_transfer_test")
            .AddInterceptors(interceptor)
            .Options;

        await using var context = new StockFlowDbContext(options);
        var repository = new TransferRepository(context);

        await repository.SaveDraftAsync(transfer);

        Assert.Equal(1, interceptor.CallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveDraftAsync_WithTrackedEdit_StagesChangesAndHandlesConflict(
        bool simulateConflict)
    {
        var transfer = new Transfer(
            Guid.NewGuid(), "BRU", "Brussels",
            Guid.NewGuid(), "LIE", "Liège",
            new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero),
            Guid.NewGuid());

        var retainedProductId = Guid.NewGuid();
        transfer.AddLine(retainedProductId, "CHAIR", "Office chair", 4);
        transfer.AddLine(Guid.NewGuid(), "DESK", "Office desk", 2);

        var retained = transfer.Lines[0];
        var removed = transfer.Lines[1];
        var addedProductId = Guid.NewGuid();
        var originalVersion = Guid.NewGuid();

        var interceptor = new InspectEditSaveInterceptor(context =>
        {
            var header = context.Entry(transfer);
            var version = header.Property<Guid>("Version");
            Assert.Equal(EntityState.Modified, header.State);
            Assert.True(version.IsModified);
            Assert.Equal(originalVersion, version.OriginalValue);
            Assert.NotEqual(originalVersion, version.CurrentValue);
            Assert.NotEqual(Guid.Empty, version.CurrentValue);

            var retainedEntry = context.Entry(retained);
            Assert.Equal(EntityState.Modified, retainedEntry.State);
            Assert.True(retainedEntry.Property(line => line.Quantity).IsModified);
            Assert.Equal(4, retainedEntry.Property(line => line.Quantity).OriginalValue);
            Assert.Equal(8, retainedEntry.Property(line => line.Quantity).CurrentValue);
            Assert.Equal(EntityState.Deleted, context.Entry(removed).State);

            var added = Assert.Single(
                context.ChangeTracker.Entries<TransferLine>(),
                entry => entry.Entity.ProductId == addedProductId);
            Assert.Equal(EntityState.Added, added.State);
            Assert.Equal(transfer.Id, added.Property<Guid>("TransferId").CurrentValue);
            Assert.Empty(context.ChangeTracker.Entries<StockMovement>());

            if (simulateConflict)
            {
                throw new DbUpdateConcurrencyException("Simulated stale transfer.");
            }
        });

        var options = new DbContextOptionsBuilder<StockFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=unused_transfer_test")
            .AddInterceptors(interceptor)
            .Options;

        await using var context = new StockFlowDbContext(options);
        var version = context.Transfers.Attach(transfer).Property<Guid>("Version");
        version.CurrentValue = originalVersion;
        version.OriginalValue = originalVersion;
        version.IsModified = false;

        transfer.ReplaceDraftLines(
        [
            new(retainedProductId, "CHAIR", "Office chair", 8),
            new(addedProductId, "LAMP", "Desk lamp", 3)
        ]);

        var repository = new TransferRepository(context);

        if (simulateConflict)
        {
            var error = await Assert.ThrowsAsync<TransferConflictException>(
                () => repository.SaveDraftAsync(transfer));
            Assert.Equal(transfer.Id, error.TransferId);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.SaveDraftAsync(transfer));
        }
        else
        {
            await repository.SaveDraftAsync(transfer);
        }

        Assert.True(context.ChangeTracker.AutoDetectChangesEnabled);
        Assert.Equal(1, interceptor.CallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveCancellationAsync_PreservesLinesAndChecksConcurrency(
        bool simulateConflict)
    {
        var createdAt = new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);
        var transfer = new Transfer(
            Guid.NewGuid(), "BRU", "Brussels",
            Guid.NewGuid(), "LIE", "Liège", createdAt, Guid.NewGuid());
        transfer.AddLine(Guid.NewGuid(), "CHAIR", "Office chair", 4);

        var originalVersion = Guid.NewGuid();
        var cancelledAt = createdAt.AddHours(1);
        var actorId = Guid.NewGuid();

        var interceptor = new InspectEditSaveInterceptor(context =>
        {
            var header = context.Entry(transfer);
            Assert.Equal(EntityState.Modified, header.State);
            Assert.Equal(TransferStatus.Draft,
                header.Property(item => item.Status).OriginalValue);
            Assert.Equal(TransferStatus.Cancelled, transfer.Status);
            Assert.Equal(cancelledAt, transfer.CancelledAt);
            Assert.Equal(actorId, transfer.CancelledByUserId);

            var version = header.Property<Guid>("Version");
            Assert.True(version.IsModified);
            Assert.Equal(originalVersion, version.OriginalValue);
            Assert.NotEqual(originalVersion, version.CurrentValue);
            Assert.NotEqual(Guid.Empty, version.CurrentValue);

            var line = Assert.Single(context.ChangeTracker.Entries<TransferLine>());
            Assert.Equal(EntityState.Unchanged, line.State);
            Assert.Equal(4, line.Entity.Quantity);
            Assert.Empty(context.ChangeTracker.Entries<StockMovement>());
            Assert.Empty(context.ChangeTracker.Entries<StockFlow.Domain.Entities.Inventory>());

            if (simulateConflict)
            {
                throw new DbUpdateConcurrencyException("Simulated stale transfer.");
            }
        });

        var options = new DbContextOptionsBuilder<StockFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=unused_transfer_test")
            .AddInterceptors(interceptor)
            .Options;

        await using var context = new StockFlowDbContext(options);
        var version = context.Transfers.Attach(transfer).Property<Guid>("Version");
        version.CurrentValue = originalVersion;
        version.OriginalValue = originalVersion;
        version.IsModified = false;

        transfer.Cancel(cancelledAt, actorId);
        var repository = new TransferRepository(context);

        if (simulateConflict)
        {
            var error = await Assert.ThrowsAsync<TransferConflictException>(
                () => repository.SaveCancellationAsync(transfer));
            Assert.Equal(transfer.Id, error.TransferId);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.SaveCancellationAsync(transfer));
        }
        else
        {
            await repository.SaveCancellationAsync(transfer);
        }

        Assert.Equal(1, interceptor.CallCount);
    }

    private sealed class InspectEditSaveInterceptor(
        Action<StockFlowDbContext> inspect) : SaveChangesInterceptor
    {
        public int CallCount { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            inspect(Assert.IsType<StockFlowDbContext>(eventData.Context));
            return ValueTask.FromResult(
                InterceptionResult<int>.SuppressWithResult(4));
        }
    }

    private sealed class InspectDraftSaveInterceptor(Transfer expectedTransfer)
        : SaveChangesInterceptor
    {
        public int CallCount { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var context = Assert.IsType<StockFlowDbContext>(eventData.Context);
            var header = Assert.Single(context.ChangeTracker.Entries<Transfer>());

            Assert.Same(expectedTransfer, header.Entity);
            Assert.Equal(EntityState.Added, header.State);
            Assert.NotEqual(Guid.Empty, header.Property<Guid>("Version").CurrentValue);

            var lines = context.ChangeTracker.Entries<TransferLine>().ToArray();
            Assert.Equal(2, lines.Length);

            Assert.All(lines, line =>
            {
                Assert.Equal(EntityState.Added, line.State);
                Assert.Equal(
                    expectedTransfer.Id,
                    line.Property<Guid>("TransferId").CurrentValue);
            });

            Assert.Empty(context.ChangeTracker.Entries<StockMovement>());

            return ValueTask.FromResult(
                InterceptionResult<int>.SuppressWithResult(3));
        }
    }
}
