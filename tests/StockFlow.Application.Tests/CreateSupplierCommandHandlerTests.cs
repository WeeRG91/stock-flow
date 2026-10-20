using StockFlow.Application.Exceptions;
using StockFlow.Application.Suppliers.CreateSupplier;
using StockFlow.Application.Tests.Fakes;
using StockFlow.Domain.Entities;

namespace StockFlow.Application.Tests;

public class CreateSupplierCommandHandlerTests
{
    private static CreateSupplierCommand ValidCommand() =>
        new("SUP-001", "Supplier", "sales@example.com");

    [Fact]
    public void Constructor_WithNullRepository_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new CreateSupplierCommandHandler(null!));
    }

    [Fact]
    public async Task HandleAsync_CreatesNormalizedSupplierAndReturnsDetails()
    {
        var repository = new FakeSupplierRepository();
        var handler = new CreateSupplierCommandHandler(repository);
        using var cancellation = new CancellationTokenSource();

        var command = new CreateSupplierCommand(
            "  sup-001  ",
            "  Office Supplier  ",
            "  Sales@Example.com  ",
            "  +352 020 30 40  ");

        var result = await handler.HandleAsync(
            command, cancellation.Token);

        var saved = Assert.IsType<Supplier>(
            repository.LastSavedSupplier);

        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.Equal("SUP-001", saved.Code);
        Assert.Equal("Office Supplier", saved.Name);
        Assert.Equal("Sales@Example.com", saved.ContactEmail);
        Assert.Equal("+352 020 30 40", saved.PhoneNumber);
        Assert.True(saved.IsActive);

        Assert.Equal(
            new CreateSupplierResult(
                saved.Id,
                saved.Code,
                saved.Name,
                saved.ContactEmail,
                saved.PhoneNumber,
                saved.IsActive),
            result);

        Assert.Equal("SUP-001", repository.LastCheckedCode);
        Assert.Equal(1, repository.ExistsCallCount);
        Assert.Equal(1, repository.SaveCallCount);
        Assert.Equal(cancellation.Token, repository.LookupToken);
        Assert.Equal(cancellation.Token, repository.SaveToken);
    }

    [Fact]
    public async Task HandleAsync_WithoutPhone_ReturnsNullPhone()
    {
        var repository = new FakeSupplierRepository();
        var handler = new CreateSupplierCommandHandler(repository);

        var result = await handler.HandleAsync(ValidCommand());

        Assert.Null(result.PhoneNumber);
    }

    [Fact]
    public async Task HandleAsync_WithNullCommand_DoesNotAccessRepository()
    {
        var repository = new FakeSupplierRepository();
        var handler = new CreateSupplierCommandHandler(repository);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.HandleAsync(null!));

        Assert.Equal(0, repository.ExistsCallCount);
        Assert.Equal(0, repository.SaveCallCount);
    }

    [Theory]
    [InlineData("Code", null)]
    [InlineData("Code", "")]
    [InlineData("Code", "   ")]
    [InlineData("Name", null)]
    [InlineData("Name", "")]
    [InlineData("Name", "   ")]
    [InlineData("ContactEmail", null)]
    [InlineData("ContactEmail", "")]
    [InlineData("ContactEmail", "   ")]
    [InlineData("ContactEmail", "not-an-email")]
    public async Task HandleAsync_WithInvalidInput_ReturnsFieldValidation(
        string field,
        string? value)
    {
        var repository = new FakeSupplierRepository();
        var handler = new CreateSupplierCommandHandler(repository);

        var command = new CreateSupplierCommand(
            field == "Code" ? value! : "SUP-001",
            field == "Name" ? value! : "Supplier",
            field == "ContactEmail" ? value! : "sales@example.com");

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => handler.HandleAsync(command));

        Assert.Equal(field, exception.FieldName);
        Assert.Equal(0, repository.ExistsCallCount);
        Assert.Equal(0, repository.SaveCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_WithExistingCode_RejectsEvenIfInactive(
        bool inactive)
    {
        var repository = new FakeSupplierRepository();
        var existing = new Supplier(
            "SUP-001", "Existing supplier", "existing@example.com");

        if (inactive)
        {
            existing.Deactivate();
        }

        repository.Add(existing);
        var handler = new CreateSupplierCommandHandler(repository);

        var exception =
            await Assert.ThrowsAsync<SupplierCodeConflictException>(
                () => handler.HandleAsync(
                    new CreateSupplierCommand(
                        "  sup-001  ",
                        "Another supplier",
                        "another@example.com")));

        Assert.Equal("SUP-001", exception.Code);
        Assert.Equal(1, repository.ExistsCallCount);
        Assert.Equal(0, repository.SaveCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenAlreadyCancelled_DoesNotAccessRepository()
    {
        var repository = new FakeSupplierRepository();
        var handler = new CreateSupplierCommandHandler(repository);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(
                ValidCommand(), cancellation.Token));

        Assert.Equal(0, repository.ExistsCallCount);
        Assert.Equal(0, repository.SaveCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_WhenRepositoryFails_DoesNotReclassifyError(
        bool duringSave)
    {
        var repository = new FakeSupplierRepository();

        // The parameter name deliberately matches a Domain validation field.
        var failure = new ArgumentException(
            "Simulated repository failure.", "code");

        if (duringSave)
            repository.SaveException = failure;
        else
            repository.LookupException = failure;

        var handler = new CreateSupplierCommandHandler(repository);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(ValidCommand()));

        Assert.Same(failure, exception);
        Assert.Equal(duringSave ? 1 : 0, repository.SaveCallCount);
        Assert.Null(repository.LastSavedSupplier);
    }

    [Fact]
    public async Task HandleAsync_WhenSaveReportsCodeConflict_PropagatesConflict()
    {
        var failure = new SupplierCodeConflictException("SUP-001");
        var repository = new FakeSupplierRepository
        {
            SaveException = failure
        };
        var handler = new CreateSupplierCommandHandler(repository);

        var exception =
            await Assert.ThrowsAsync<SupplierCodeConflictException>(
                () => handler.HandleAsync(ValidCommand()));

        Assert.Same(failure, exception);
        Assert.Equal(1, repository.ExistsCallCount);
        Assert.Equal(1, repository.SaveCallCount);
        Assert.Null(repository.LastSavedSupplier);
    }

    [Theory]
    [InlineData("Code")]
    [InlineData("Name")]
    [InlineData("ContactEmail")]
    [InlineData("PhoneNumber")]
    public async Task HandleAsync_WithOversizedField_RejectsBeforeRepositoryAccess(
    string field)
    {
        var repository = new FakeSupplierRepository();
        var handler = new CreateSupplierCommandHandler(repository);

        var oversizedEmail =
            new string('a', 64) + "@" +
            new string('b', 63) + "." +
            new string('c', 63) + "." +
            new string('d', 62);

        var command = new CreateSupplierCommand(
            field == "Code"
                ? new string('A', Supplier.MaxCodeLength + 1)
                : "SUP-001",
            field == "Name"
                ? new string('N', Supplier.MaxNameLength + 1)
                : "Supplier",
            field == "ContactEmail"
                ? oversizedEmail
                : "sales@example.com",
            field == "PhoneNumber"
                ? new string('1', Supplier.MaxPhoneNumberLength + 1)
                : null);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => handler.HandleAsync(command));

        Assert.Equal(field, exception.FieldName);
        Assert.Equal(0, repository.ExistsCallCount);
        Assert.Equal(0, repository.SaveCallCount);
    }
}