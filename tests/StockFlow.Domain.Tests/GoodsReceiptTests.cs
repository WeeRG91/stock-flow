using StockFlow.Domain.Entities;

namespace StockFlow.Domain.Tests;

public class GoodsReceiptTests
{
    [Fact]
    public void Constructor_WithNullLines_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>("lines", () => new GoodsReceipt(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero),
            Guid.NewGuid(),
            null!));
    }

    [Fact]
    public void Constructor_WithNullLine_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>("lines", () => new GoodsReceipt(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero),
            Guid.NewGuid(),
            new GoodsReceiptLine[] { null! }));
    }

    [Fact]
    public void Constructor_WhenCallerClearsInputList_PreservesReceiptLines()
    {
        var line = new GoodsReceiptLine(Guid.NewGuid(), 4);
        var inputLines = new List<GoodsReceiptLine> { line };

        var receipt = new GoodsReceipt(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero),
            Guid.NewGuid(),
            inputLines);

        inputLines.Clear();

        var savedLine = Assert.Single(receipt.Lines);
        Assert.Equal(line.PurchaseOrderLineId, savedLine.PurchaseOrderLineId);
        Assert.Equal(4, savedLine.ReceivedQuantity);
    }

    [Fact]
    public void Constructor_WithDuplicateOrderLineIds_ThrowsArgumentException()
    {
        var orderLineId = Guid.NewGuid();
        var lines = new[]
        {
        new GoodsReceiptLine(orderLineId, 4),
        new GoodsReceiptLine(orderLineId, 2)
    };

        var exception = Assert.Throws<ArgumentException>(() => new GoodsReceipt(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero),
            Guid.NewGuid(),
            lines));

        Assert.Equal("lines", exception.ParamName);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("purchaseOrderId")]
    [InlineData("confirmedByUserId")]
    public void Constructor_WithEmptyIdentifier_ThrowsArgumentException(
    string emptyParameter)
    {
        var receiptId = emptyParameter == "id" ? Guid.Empty : Guid.NewGuid();
        var orderId = emptyParameter == "purchaseOrderId" ? Guid.Empty : Guid.NewGuid();
        var userId = emptyParameter == "confirmedByUserId" ? Guid.Empty : Guid.NewGuid();

        Assert.Throws<ArgumentException>(emptyParameter, () => new GoodsReceipt(
            receiptId,
            orderId,
            new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero),
            userId,
            new[] { new GoodsReceiptLine(Guid.NewGuid(), 4) }));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WithInvalidConfirmationTime_ThrowsArgumentException(
    bool useDefaultTimestamp)
    {
        DateTimeOffset confirmedAt = useDefaultTimestamp
            ? default
            : new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.FromHours(1));

        Assert.Throws<ArgumentException>("confirmedAt", () => new GoodsReceipt(
            Guid.NewGuid(),
            Guid.NewGuid(),
            confirmedAt,
            Guid.NewGuid(),
            new[] { new GoodsReceiptLine(Guid.NewGuid(), 4) }));
    }

    [Fact]
    public void Constructor_WithNoLines_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>("lines", () => new GoodsReceipt(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero),
            Guid.NewGuid(),
            Array.Empty<GoodsReceiptLine>()));
    }
}
