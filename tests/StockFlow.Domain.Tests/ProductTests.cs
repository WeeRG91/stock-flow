using StockFlow.Domain.Entities;

namespace StockFlow.Domain.Tests;

public class ProductTests
{
    [Fact]
    public void Constructor_WithValidInput_CreatesNormalizedActiveProduct()
    {
        // Act
        var product = new Product(
            "  Office Chair  ",
            "  chair-blk  ",
            "  Black office chair  ");

        // Assert
        Assert.NotEqual(Guid.Empty, product.Id);
        Assert.Equal("Office Chair", product.Name);
        Assert.Equal("CHAIR-BLK", product.Sku);
        Assert.Equal("Black office chair", product.Description);
        Assert.True(product.IsActive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Constructor_WithInvalidName_ThrowsArgumentException(
    string? name)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new Product(name!, "CHAIR-BLK"));

        Assert.Equal("name", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Constructor_WithInvalidSku_ThrowsArgumentException(
        string? sku)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new Product("Office Chair", sku!));

        Assert.Equal("sku", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Constructor_WithMissingDescription_NormalizesToNull(
        string? description)
    {
        var product = new Product(
            "Office Chair",
            "CHAIR-BLK",
            description);

        Assert.Null(product.Description);
    }

    [Fact]
    public void Constructor_WithoutDescription_CreatesProduct()
    {
        var product = new Product("Office Chair", "CHAIR-BLK");

        Assert.Null(product.Description);
        Assert.True(product.IsActive);
    }

    [Fact]
    public void Deactivate_PreservesProductDetails()
    {
        // Arrange
        var product = new Product(
            "Office Chair",
            "CHAIR-BLK",
            "Black office chair");
        var originalId = product.Id;

        // Act
        product.Deactivate();

        // Assert
        Assert.False(product.IsActive);
        Assert.Equal(originalId, product.Id);
        Assert.Equal("Office Chair", product.Name);
        Assert.Equal("CHAIR-BLK", product.Sku);
        Assert.Equal("Black office chair", product.Description);
    }

    [Fact]
    public void Activate_AfterDeactivation_PreservesIdentity()
    {
        // Arrange
        var product = new Product("Office Chair", "CHAIR-BLK");
        var originalId = product.Id;
        product.Deactivate();

        // Act
        product.Activate();

        // Assert
        Assert.True(product.IsActive);
        Assert.Equal(originalId, product.Id);
    }

    [Fact]
    public void LifecycleMethods_CanBeCalledRepeatedly()
    {
        var product = new Product("Office Chair", "CHAIR-BLK");

        product.Activate();
        product.Activate();
        Assert.True(product.IsActive);

        product.Deactivate();
        product.Deactivate();
        Assert.False(product.IsActive);

        product.Activate();
        product.Activate();
        Assert.True(product.IsActive);
    }

    [Fact]
    public void Deactivate_PreservesExistingInventoryAndMovements()
    {
        // Arrange
        var product = new Product("Office Chair", "CHAIR-BLK");
        var inventory = new Inventory(product.Id, Guid.NewGuid());
        var occurredAt = new DateTimeOffset(
            2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

        inventory.ReceiveStock(10, "Initial stock", occurredAt);
        var movementsBefore = inventory.PendingMovements.ToArray();

        // Act
        product.Deactivate();

        // Assert
        Assert.False(product.IsActive);
        Assert.Equal(product.Id, inventory.ProductId);
        Assert.Equal(10, inventory.QuantityOnHand);
        Assert.Equal(movementsBefore, inventory.PendingMovements.ToArray());
    }
}