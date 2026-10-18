using StockFlow.Domain.Entities;

namespace StockFlow.Domain.Tests;

public class SupplierTests
{
    [Fact]
    public void Constructor_WithValidInput_NormalizesAndActivatesSupplier()
    {
        var supplier = new Supplier(
            "  sup-001  ",
            "  Nova Office Supplies  ",
            "  Sales@Example.com  ",
            "  +352 020 30 40  ");

        Assert.NotEqual(Guid.Empty, supplier.Id);
        Assert.Equal("SUP-001", supplier.Code);
        Assert.Equal("Nova Office Supplies", supplier.Name);
        Assert.Equal("Sales@Example.com", supplier.ContactEmail);
        Assert.Equal("+352 020 30 40", supplier.PhoneNumber);
        Assert.True(supplier.IsActive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Constructor_WithMissingCode_Throws(string? code)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new Supplier(code!, "Supplier", "sales@example.com"));

        Assert.Equal("code", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Constructor_WithMissingName_Throws(string? name)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new Supplier("SUP-001", name!, "sales@example.com"));

        Assert.Equal("name", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Constructor_WithMissingEmail_Throws(string? email)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new Supplier("SUP-001", "Supplier", email!));

        Assert.Equal("contactEmail", exception.ParamName);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@example.com")]
    [InlineData("sales@")]
    [InlineData("sales@@example.com")]
    [InlineData("Sales Team <sales@example.com>")]
    [InlineData("<sales@example.com>")]
    [InlineData("sales@example.com,other@example.com")]
    [InlineData("sales\r\n@example.com")]
    public void Constructor_WithInvalidEmailFormat_Throws(string email)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new Supplier("SUP-001", "Supplier", email));

        Assert.Equal("contactEmail", exception.ParamName);
    }

    [Theory]
    [InlineData("sales+orders@suppliers.example.com")]
    [InlineData("Sales@Example.com")]
    [InlineData("sales@localhost")]
    public void Constructor_WithSupportedEmail_PreservesAddress(string email)
    {
        var supplier = new Supplier("SUP-001", "Supplier", email);

        Assert.Equal(email, supplier.ContactEmail);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Constructor_WithMissingPhone_NormalizesToNull(string? phone)
    {
        var supplier = new Supplier(
            "SUP-001", "Supplier", "sales@example.com", phone);

        Assert.Null(supplier.PhoneNumber);
    }

    [Fact]
    public void Constructor_WithoutPhone_CreatesSupplier()
    {
        var supplier = new Supplier(
            "SUP-001", "Supplier", "sales@example.com");

        Assert.Null(supplier.PhoneNumber);
        Assert.True(supplier.IsActive);
    }

    [Fact]
    public void Deactivate_PreservesIdentityAndContactDetails()
    {
        var supplier = new Supplier(
            "SUP-001",
            "Supplier",
            "sales@example.com",
            "+352 020 30 40");
        var originalId = supplier.Id;

        supplier.Deactivate();

        Assert.False(supplier.IsActive);
        Assert.Equal(originalId, supplier.Id);
        Assert.Equal("SUP-001", supplier.Code);
        Assert.Equal("Supplier", supplier.Name);
        Assert.Equal("sales@example.com", supplier.ContactEmail);
        Assert.Equal("+352 020 30 40", supplier.PhoneNumber);
    }

    [Fact]
    public void Activate_AfterDeactivation_PreservesIdentityAndDetails()
    {
        var supplier = new Supplier(
            "SUP-001",
            "Supplier",
            "sales@example.com",
            "+352 020 30 40");
        var originalId = supplier.Id;
        supplier.Deactivate();

        supplier.Activate();

        Assert.True(supplier.IsActive);
        Assert.Equal(originalId, supplier.Id);
        Assert.Equal("SUP-001", supplier.Code);
        Assert.Equal("Supplier", supplier.Name);
        Assert.Equal("sales@example.com", supplier.ContactEmail);
        Assert.Equal("+352 020 30 40", supplier.PhoneNumber);
    }

    [Fact]
    public void LifecycleMethods_TolerateRepeatedCalls()
    {
        var supplier = new Supplier(
            "SUP-001", "Supplier", "sales@example.com");

        supplier.Activate();
        supplier.Activate();
        Assert.True(supplier.IsActive);

        supplier.Deactivate();
        supplier.Deactivate();
        Assert.False(supplier.IsActive);

        supplier.Activate();
        supplier.Activate();
        Assert.True(supplier.IsActive);
    }
}