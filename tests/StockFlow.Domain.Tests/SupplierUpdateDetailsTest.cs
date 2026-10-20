using StockFlow.Domain.Entities;

namespace StockFlow.Domain.Tests;

public class SupplierUpdateDetailsTests
{
    private static Supplier CreateSupplier() =>
        new("SUP-001", "Original Supplier", "original@example.com", "123");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UpdateDetails_NormalizesValuesAndPreservesIdentityAndStatus(
        bool isActive)
    {
        var supplier = CreateSupplier();
        var originalId = supplier.Id;
        var originalCode = supplier.Code;

        if (!isActive)
            supplier.Deactivate();

        supplier.UpdateDetails(
            "  Updated Supplier  ",
            "  Sales@example.com  ",
            "  +352 456  ");

        Assert.Equal("Updated Supplier", supplier.Name);
        Assert.Equal("Sales@example.com", supplier.ContactEmail);
        Assert.Equal("+352 456", supplier.PhoneNumber);
        Assert.Equal(originalId, supplier.Id);
        Assert.Equal(originalCode, supplier.Code);
        Assert.Equal(isActive, supplier.IsActive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDetails_WithBlankPhone_ClearsExistingPhone(string? phone)
    {
        var supplier = CreateSupplier();

        supplier.UpdateDetails("Updated", "updated@example.com", phone);

        Assert.Null(supplier.PhoneNumber);
    }

    [Fact]
    public void UpdateDetails_WithOmittedPhone_ClearsExistingPhone()
    {
        var supplier = CreateSupplier();

        supplier.UpdateDetails("Updated", "updated@example.com");

        Assert.Null(supplier.PhoneNumber);
    }

    public static IEnumerable<object?[]> InvalidDetails()
    {
        yield return new object?[] { null, "new@example.com", "456", "name" };
        yield return new object?[] { "", "new@example.com", "456", "name" };
        yield return new object?[] { "   ", "new@example.com", "456", "name" };
        yield return new object?[] { "Updated", null, "456", "contactEmail" };
        yield return new object?[] { "Updated", "", "456", "contactEmail" };
        yield return new object?[] { "Updated", "   ", "456", "contactEmail" };
        yield return new object?[] { "Updated", "invalid", "456", "contactEmail" };
        yield return new object?[]
        {
            "Updated", "Sales <sales@example.com>", "456", "contactEmail"
        };
        yield return new object?[]
        {
            "Updated", "first@example.com;second@example.com",
            "456", "contactEmail"
        };
        yield return new object?[]
        {
            "Updated", "sales@\nexample.com", "456", "contactEmail"
        };
        yield return new object?[]
        {
            new string('N', Supplier.MaxNameLength + 1),
            "new@example.com", "456", "name"
        };
        yield return new object?[]
        {
            "Updated",
            new string('a', Supplier.MaxContactEmailLength) + "@example.com",
            "456", "contactEmail"
        };
        yield return new object?[]
        {
            "Updated", "new@example.com",
            new string('1', Supplier.MaxPhoneNumberLength + 1), "phoneNumber"
        };
    }

    [Theory]
    [MemberData(nameof(InvalidDetails))]
    public void UpdateDetails_WithInvalidInput_PreservesAllState(
        string? name,
        string? email,
        string? phone,
        string expectedParameter)
    {
        var supplier = CreateSupplier();
        supplier.Deactivate();
        var originalId = supplier.Id;

        var exception = Assert.Throws<ArgumentException>(
            () => supplier.UpdateDetails(name!, email!, phone));

        Assert.Equal(expectedParameter, exception.ParamName);
        Assert.Equal(originalId, supplier.Id);
        Assert.Equal("SUP-001", supplier.Code);
        Assert.Equal("Original Supplier", supplier.Name);
        Assert.Equal("original@example.com", supplier.ContactEmail);
        Assert.Equal("123", supplier.PhoneNumber);
        Assert.False(supplier.IsActive);
    }

    [Fact]
    public void UpdateDetails_ValidatesLengthsAfterTrimming()
    {
        var supplier = CreateSupplier();
        var name = new string('N', Supplier.MaxNameLength);
        var phone = new string('1', Supplier.MaxPhoneNumberLength);

        supplier.UpdateDetails(
            $"  {name}  ",
            "  updated@example.com  ",
            $"  {phone}  ");

        Assert.Equal(name, supplier.Name);
        Assert.Equal(phone, supplier.PhoneNumber);
        Assert.Equal("updated@example.com", supplier.ContactEmail);
    }
}