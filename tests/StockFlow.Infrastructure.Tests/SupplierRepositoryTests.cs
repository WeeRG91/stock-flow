using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using StockFlow.Infrastructure.Persistence;
using StockFlow.Infrastructure.Persistence.Repositories;

namespace StockFlow.Infrastructure.Tests;

public class SupplierRepositoryTests
{
    private static Supplier CreateSupplier() =>
        new("SUP-001", "Supplier", "sales@example.com");

    private static StockFlowDbContext CreateContext(
        SaveChangesInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<StockFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=unused_supplier_test")
            .AddInterceptors(interceptor)
            .Options;

        return new StockFlowDbContext(options);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveAsync_StagesSupplierAndRejectsReuseAfterFailure(
        bool existing)
    {
        var failure = new InvalidOperationException(
            "Simulated save failure.");
        var interceptor = new ThrowingSaveInterceptor(failure);
        await using var context = CreateContext(interceptor);
        var supplier = CreateSupplier();

        if (existing)
        {
            context.Suppliers.Attach(supplier);
            supplier.Deactivate();
        }

        var repository = new SupplierRepository(context);
        using var cancellation = new CancellationTokenSource();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.SaveAsync(supplier, cancellation.Token));

        Assert.Same(failure, exception);
        Assert.Equal(1, interceptor.CallCount);
        Assert.Equal(
            existing ? EntityState.Modified : EntityState.Added,
            interceptor.SupplierState);
        Assert.Equal(cancellation.Token, interceptor.ReceivedToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.SaveAsync(supplier));

        Assert.Equal(1, interceptor.CallCount);
    }

    [Theory]
    [InlineData("23505", "IX_Suppliers_Code", "Suppliers", true)]
    [InlineData("23505", "IX_Other", "Suppliers", false)]
    [InlineData("23503", "IX_Suppliers_Code", "Suppliers", false)]
    [InlineData("23505", "IX_Suppliers_Code", "OtherTable", false)]
    public async Task SaveAsync_TranslatesOnlySupplierCodeUniqueViolation(
        string sqlState,
        string constraintName,
        string tableName,
        bool expectedConflict)
    {
        var postgresException = new PostgresException(
            messageText: "Simulated constraint failure.",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: sqlState,
            tableName: tableName,
            constraintName: constraintName);

        var databaseException = new DbUpdateException(
            "Simulated save failure.",
            postgresException);

        var interceptor = new ThrowingSaveInterceptor(databaseException);
        await using var context = CreateContext(interceptor);
        var supplier = CreateSupplier();
        var repository = new SupplierRepository(context);

        if (expectedConflict)
        {
            var exception =
                await Assert.ThrowsAsync<SupplierCodeConflictException>(
                    () => repository.SaveAsync(supplier));

            Assert.Equal(supplier.Code, exception.Code);
        }
        else
        {
            var exception = await Assert.ThrowsAsync<DbUpdateException>(
                () => repository.SaveAsync(supplier));

            Assert.Same(databaseException, exception);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.SaveAsync(supplier));

        Assert.Equal(1, interceptor.CallCount);
    }

    [Fact]
    public async Task SaveAsync_WhenAlreadyCancelled_DoesNotStageOrSave()
    {
        var interceptor = new ThrowingSaveInterceptor(
            new InvalidOperationException("Must not reach saving."));
        await using var context = CreateContext(interceptor);
        var supplier = CreateSupplier();
        var repository = new SupplierRepository(context);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.SaveAsync(supplier, cancellation.Token));

        Assert.Equal(EntityState.Detached, context.Entry(supplier).State);
        Assert.Equal(0, interceptor.CallCount);
    }

    [Fact]
    public async Task SaveAsync_WithDeletedEntity_RejectsDeletion()
    {
        var interceptor = new ThrowingSaveInterceptor(
            new InvalidOperationException("Must not reach saving."));
        await using var context = CreateContext(interceptor);
        var supplier = CreateSupplier();

        context.Suppliers.Attach(supplier);
        context.Entry(supplier).State = EntityState.Deleted;

        var repository = new SupplierRepository(context);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.SaveAsync(supplier));

        Assert.Equal(0, interceptor.CallCount);
    }

    private sealed class ThrowingSaveInterceptor : SaveChangesInterceptor
    {
        private readonly Exception _failure;

        public int CallCount { get; private set; }
        public EntityState SupplierState { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }

        public ThrowingSaveInterceptor(Exception failure)
        {
            _failure = failure;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var context = eventData.Context
                ?? throw new InvalidOperationException("Expected a DbContext.");

            context.ChangeTracker.DetectChanges();

            CallCount++;
            ReceivedToken = cancellationToken;
            SupplierState = context.ChangeTracker
                .Entries<Supplier>()
                .Single()
                .State;

            throw _failure;
        }
    }
}