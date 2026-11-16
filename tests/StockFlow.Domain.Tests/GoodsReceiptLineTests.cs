using StockFlow.Domain.Entities;

namespace StockFlow.Domain.Tests;

public class GoodsReceiptLineTests
{
    [Fact]
    public void Constructor_WithEmptyOrderLineId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            "purchaseOrderLineId",
            () => new GoodsReceiptLine(Guid.Empty, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveQuantity_ThrowsArgumentOutOfRangeException(
        int quantity)
    {
        var orderLineId = Guid.NewGuid();

        Assert.Throws<ArgumentOutOfRangeException>(
            "receivedQuantity",
            () => new GoodsReceiptLine(orderLineId, quantity));
    }
}
