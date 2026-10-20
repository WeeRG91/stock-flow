using StockFlow.Application.Exceptions;
using StockFlow.Application.Suppliers.UpdateSupplier;
using StockFlow.Application.Tests.Fakes;
using StockFlow.Domain.Entities;

namespace StockFlow.Application.Tests;

public class UpdateSupplierCommandHandlerTests
{
    private static Supplier CreateSupplier() =>
        new("SUP-001", "Original", "original@example.com", "123");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_UpdatesExistingSupplierAndPreservesIdentity(
        bool isActive)
    {
        var supplier = CreateSupplier();
        var id = supplier.Id;

        if (!isActive)
            supplier.Deactivate();

        var repository = new FakeSupplierRepository();
        repository.Add(supplier);
        var handler = new UpdateSupplierCommandHandler(repository);
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(
            new UpdateSupplierCommand(
                id, " Updated ", " Sales@example.com ", " 456 "),
            cancellation.Token);

        Assert.Equal(id, result.Id);
        Assert.Equal("SUP-001", result.Code);
        Assert.Equal("Updated", result.Name);
        Assert.Equal("Sales@example.com", result.ContactEmail);
        Assert.Equal("456", result.PhoneNumber);
        Assert.Equal(isActive, result.IsActive);
        Assert.Same(supplier, repository.LastSavedSupplier);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(id, repository.LastRequestedSupplierId);
        Assert.Equal(1, repository.SaveCallCount);
        Assert.Equal(0, repository.ExistsCallCount);
        Assert.Equal(cancellation.Token, repository.GetByIdToken);
        Assert.Equal(cancellation.Token, repository.SaveToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_WithBlankPhone_ClearsPhone(string? phone)
    {
        var supplier = CreateSupplier();
        var repository = new FakeSupplierRepository();
        repository.Add(supplier);
        var handler = new UpdateSupplierCommandHandler(repository);

        var result = await handler.HandleAsync(
            new UpdateSupplierCommand(
                supplier.Id, "Updated", "updated@example.com", phone));

        Assert.Null(result.PhoneNumber);
        Assert.Null(supplier.PhoneNumber);
        Assert.Equal(1, repository.SaveCallCount);
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("ContactEmail")]
    [InlineData("PhoneNumber")]
    public async Task HandleAsync_WithInvalidDetails_DoesNotMutateOrSave(
        string field)
    {
        var supplier = CreateSupplier();
        var repository = new FakeSupplierRepository();
        repository.Add(supplier);
        var handler = new UpdateSupplierCommandHandler(repository);

        var command = new UpdateSupplierCommand(
            supplier.Id,
            field == "Name" ? "   " : "Updated",
            field == "ContactEmail" ? "invalid" : "updated@example.com",
            field == "PhoneNumber"
                ? new string('1', Supplier.MaxPhoneNumberLength + 1)
                : "456");

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => handler.HandleAsync(command));

        Assert.Equal(field, exception.FieldName);
        Assert.Equal("Original", supplier.Name);
        Assert.Equal("original@example.com", supplier.ContactEmail);
        Assert.Equal("123", supplier.PhoneNumber);
        Assert.Equal(0, repository.SaveCallCount);
    }

    [Fact]
    public void Constructor_WithNullRepository_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new UpdateSupplierCommandHandler(null!));
    }

    [Fact]
    public async Task HandleAsync_WithNullCommand_DoesNotReadOrSave()
    {
        var repository = new FakeSupplierRepository();
        var handler = new UpdateSupplierCommandHandler(repository);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.HandleAsync(null!));

        Assert.Equal(0, repository.GetByIdCallCount);
        Assert.Equal(0, repository.SaveCallCount);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyId_DoesNotReadOrSave()
    {
        var repository = new FakeSupplierRepository();
        var handler = new UpdateSupplierCommandHandler(repository);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => handler.HandleAsync(new UpdateSupplierCommand(
                Guid.Empty, "Updated", "updated@example.com")));

        Assert.Equal("SupplierId", exception.FieldName);
        Assert.Equal(0, repository.GetByIdCallCount);
        Assert.Equal(0, repository.SaveCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenMissing_ThrowsNotFoundWithoutSaving()
    {
        var repository = new FakeSupplierRepository();
        var handler = new UpdateSupplierCommandHandler(repository);
        var id = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => handler.HandleAsync(new UpdateSupplierCommand(
                id, "Updated", "updated@example.com")));

        Assert.Equal("Supplier", exception.ResourceName);
        Assert.Equal(id, exception.ResourceId);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(0, repository.SaveCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenCancelled_DoesNotReadOrSave()
    {
        var repository = new FakeSupplierRepository();
        var handler = new UpdateSupplierCommandHandler(repository);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(
                new UpdateSupplierCommand(
                    Guid.NewGuid(), "Updated", "updated@example.com"),
                cancellation.Token));

        Assert.Equal(0, repository.GetByIdCallCount);
        Assert.Equal(0, repository.SaveCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_WhenRepositoryFails_DoesNotTranslateFailure(
        bool duringSave)
    {
        var supplier = CreateSupplier();
        var repository = new FakeSupplierRepository();
        repository.Add(supplier);

        // Even an ArgumentException from persistence must not become
        // a Domain input-validation error.
        var failure = new ArgumentException(
            "Simulated persistence failure", "name");

        if (duringSave)
            repository.SaveException = failure;
        else
            repository.LookupException = failure;

        var handler = new UpdateSupplierCommandHandler(repository);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(new UpdateSupplierCommand(
                supplier.Id, "Updated", "updated@example.com")));

        Assert.Same(failure, exception);
        Assert.Equal(duringSave ? 1 : 0, repository.SaveCallCount);
        Assert.Null(repository.LastSavedSupplier);
    }
}