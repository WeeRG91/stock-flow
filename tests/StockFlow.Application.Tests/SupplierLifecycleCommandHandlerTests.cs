using StockFlow.Application.Exceptions;
using StockFlow.Application.Suppliers;
using StockFlow.Application.Suppliers.ActivateSupplier;
using StockFlow.Application.Suppliers.DeactivateSupplier;
using StockFlow.Application.Tests.Fakes;
using StockFlow.Domain.Entities;

namespace StockFlow.Application.Tests;

public class SupplierLifecycleCommandHandlerTests
{
    private static Task<SupplierDto> ExecuteAsync(
        bool activate,
        FakeSupplierRepository repository,
        Guid supplierId,
        CancellationToken cancellationToken = default)
    {
        return activate
            ? new ActivateSupplierCommandHandler(repository).HandleAsync(
                new ActivateSupplierCommand(supplierId), cancellationToken)
            : new DeactivateSupplierCommandHandler(repository).HandleAsync(
                new DeactivateSupplierCommand(supplierId), cancellationToken);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task HandleAsync_SetsStatusAndPreservesDetails(
        bool activate,
        bool initiallyActive)
    {
        var supplier = new Supplier(
            "SUP-001", "Office Supplier", "office@example.com", "123");
        var id = supplier.Id;

        if (!initiallyActive)
            supplier.Deactivate();

        var repository = new FakeSupplierRepository();
        repository.Add(supplier);
        using var cancellation = new CancellationTokenSource();

        var result = await ExecuteAsync(
            activate, repository, id, cancellation.Token);

        Assert.Equal(activate, supplier.IsActive);
        Assert.Equal(activate, result.IsActive);
        Assert.Equal(id, result.Id);
        Assert.Equal("SUP-001", result.Code);
        Assert.Equal("Office Supplier", result.Name);
        Assert.Equal("office@example.com", result.ContactEmail);
        Assert.Equal("123", result.PhoneNumber);
        Assert.Same(supplier, repository.LastSavedSupplier);
        Assert.Equal(id, repository.LastRequestedSupplierId);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(1, repository.SaveCallCount);
        Assert.Equal(0, repository.ExistsCallCount);
        Assert.Equal(cancellation.Token, repository.GetByIdToken);
        Assert.Equal(cancellation.Token, repository.SaveToken);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WithNullRepository_Throws(bool activate)
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            if (activate)
                _ = new ActivateSupplierCommandHandler(null!);
            else
                _ = new DeactivateSupplierCommandHandler(null!);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_WithNullCommand_DoesNotReadOrSave(
        bool activate)
    {
        var repository = new FakeSupplierRepository();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            activate
                ? new ActivateSupplierCommandHandler(repository)
                    .HandleAsync(null!)
                : new DeactivateSupplierCommandHandler(repository)
                    .HandleAsync(null!));

        Assert.Equal(0, repository.GetByIdCallCount);
        Assert.Equal(0, repository.SaveCallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_WithEmptyId_DoesNotReadOrSave(bool activate)
    {
        var repository = new FakeSupplierRepository();

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => ExecuteAsync(activate, repository, Guid.Empty));

        Assert.Equal("SupplierId", exception.FieldName);
        Assert.Equal(0, repository.GetByIdCallCount);
        Assert.Equal(0, repository.SaveCallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_WhenMissing_DoesNotSave(bool activate)
    {
        var repository = new FakeSupplierRepository();
        var id = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => ExecuteAsync(activate, repository, id));

        Assert.Equal("Supplier", exception.ResourceName);
        Assert.Equal(id, exception.ResourceId);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(0, repository.SaveCallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_WhenCancelled_DoesNotReadOrSave(bool activate)
    {
        var repository = new FakeSupplierRepository();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ExecuteAsync(
                activate, repository, Guid.NewGuid(), cancellation.Token));

        Assert.Equal(0, repository.GetByIdCallCount);
        Assert.Equal(0, repository.SaveCallCount);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task HandleAsync_WhenRepositoryFails_PropagatesFailure(
        bool activate,
        bool duringSave)
    {
        var supplier = new Supplier(
            "SUP-001", "Office Supplier", "office@example.com");
        var repository = new FakeSupplierRepository();
        repository.Add(supplier);

        var failure = new InvalidOperationException("Simulated failure");

        if (duringSave)
            repository.SaveException = failure;
        else
            repository.LookupException = failure;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ExecuteAsync(activate, repository, supplier.Id));

        Assert.Same(failure, exception);
        Assert.Equal(duringSave ? 1 : 0, repository.SaveCallCount);
        Assert.Null(repository.LastSavedSupplier);
    }
}