using StockFlow.Domain.Entities;

namespace StockFlow.Domain.Tests;

public sealed class TransferReceiptTests
{
    private static readonly DateTimeOffset ConfirmedAt =
        new(2026, 11, 19, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WhenCallerClearsInput_PreservesReceiptLines()
    {
        var line = new TransferReceiptLine(Guid.NewGuid(), 4);
        var input = new List<TransferReceiptLine> { line };

        var receipt = CreateReceipt(input);
        input.Clear();

        Assert.Same(line, Assert.Single(receipt.Lines));
    }

    [Fact]
    public void Constructor_WithDuplicateLineIds_RejectsReceipt()
    {
        var lineId = Guid.NewGuid();

        Assert.Throws<ArgumentException>("lines", () => CreateReceipt(
            new[]
            {
                new TransferReceiptLine(lineId, 4),
                new TransferReceiptLine(lineId, 2)
            }));
    }

    [Fact]
    public void Constructor_WithNoLines_RejectsReceipt()
    {
        Assert.Throws<ArgumentException>(
            "lines", () => CreateReceipt(Array.Empty<TransferReceiptLine>()));
    }

    [Fact]
    public void Constructor_WithNullLines_RejectsReceipt()
    {
        Assert.Throws<ArgumentNullException>(
            "lines", () => CreateReceipt(null!));
    }

    [Fact]
    public void Constructor_WithNullLine_RejectsReceipt()
    {
        Assert.Throws<ArgumentException>(
            "lines", () => CreateReceipt(new TransferReceiptLine[] { null! }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReceiptLine_WithNonPositiveQuantity_RejectsQuantity(int quantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "receivedQuantity",
            () => new TransferReceiptLine(Guid.NewGuid(), quantity));
    }

    [Fact]
    public void ReceiptLine_WithEmptyLineId_RejectsIdentifier()
    {
        Assert.Throws<ArgumentException>(
            "transferLineId",
            () => new TransferReceiptLine(Guid.Empty, 1));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("transferId")]
    [InlineData("confirmedByUserId")]
    public void Constructor_WithEmptyIdentifier_RejectsThatArgument(
    string emptyParameter)
    {
        Assert.Throws<ArgumentException>(
            emptyParameter,
            () => new TransferReceipt(
                emptyParameter == "id" ? Guid.Empty : Guid.NewGuid(),
                emptyParameter == "transferId" ? Guid.Empty : Guid.NewGuid(),
                ConfirmedAt,
                emptyParameter == "confirmedByUserId" ? Guid.Empty : Guid.NewGuid(),
                new[] { new TransferReceiptLine(Guid.NewGuid(), 1) }));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WithDefaultOrNonUtcTime_RejectsConfirmation(
        bool useDefault)
    {
        var confirmedAt = useDefault
            ? default
            : ConfirmedAt.ToOffset(TimeSpan.FromHours(1));

        Assert.Throws<ArgumentException>(
            "confirmedAt",
            () => new TransferReceipt(
                Guid.NewGuid(), Guid.NewGuid(), confirmedAt, Guid.NewGuid(),
                new[] { new TransferReceiptLine(Guid.NewGuid(), 1) }));
    }

    private static TransferReceipt CreateReceipt(
        IReadOnlyList<TransferReceiptLine> lines)
    {
        return new TransferReceipt(
            Guid.NewGuid(), Guid.NewGuid(), ConfirmedAt,
            Guid.NewGuid(), lines);
    }
}