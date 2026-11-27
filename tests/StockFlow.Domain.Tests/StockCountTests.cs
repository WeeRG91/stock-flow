using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.Domain.Tests;

public sealed class StockCountTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 27, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WithValidArguments_CreatesEmptyDraft()
    {
        var warehouseId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();

        var count = new StockCount(
            warehouseId,
            "  demo-bru  ",
            "  Brussels Warehouse  ",
            "  November cycle count  ",
            CreatedAt,
            creatorId);

        Assert.NotEqual(Guid.Empty, count.Id);
        Assert.Equal(warehouseId, count.WarehouseId);
        Assert.Equal("DEMO-BRU", count.WarehouseCode);
        Assert.Equal("Brussels Warehouse", count.WarehouseName);
        Assert.Equal("November cycle count", count.Reference);
        Assert.Equal(CreatedAt, count.CreatedAt);
        Assert.Equal(creatorId, count.CreatedByUserId);
        Assert.Equal(StockCountStatus.Draft, count.Status);
        Assert.Null(count.SubmittedAt);
        Assert.Null(count.SubmittedByUserId);
        Assert.Null(count.ReviewedAt);
        Assert.Null(count.ReviewedByUserId);
        Assert.Null(count.ReviewReason);
        Assert.Null(count.AppliedAt);
        Assert.Null(count.AppliedByUserId);
        Assert.Empty(count.Lines);
    }

    [Fact]
    public void Constructor_ForSeparateSessions_AssignsDistinctIdentities()
    {
        var warehouseId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();

        var first = CreateCount(warehouseId, creatorId);
        var second = CreateCount(warehouseId, creatorId);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData(true, "warehouseId")]
    [InlineData(false, "createdByUserId")]
    public void Constructor_WithEmptyIdentifier_Rejects(bool warehouse, string parameter)
    {
        Assert.Throws<ArgumentException>(parameter, () => CreateCount(
            warehouse ? Guid.Empty : Guid.NewGuid(),
            warehouse ? Guid.NewGuid() : Guid.Empty));
    }

    [Theory]
    [InlineData("warehouseCode", null)]
    [InlineData("warehouseCode", "")]
    [InlineData("warehouseCode", " \t\r\n ")]
    [InlineData("warehouseName", null)]
    [InlineData("warehouseName", "")]
    [InlineData("warehouseName", " \t\r\n ")]
    [InlineData("reference", null)]
    [InlineData("reference", "")]
    [InlineData("reference", " \t\r\n ")]
    public void Constructor_WithMissingRequiredText_Rejects(string parameter, string? value)
    {
        Assert.Throws<ArgumentException>(parameter, () => new StockCount(
            Guid.NewGuid(),
            parameter == "warehouseCode" ? value! : "DEMO-BRU",
            parameter == "warehouseName" ? value! : "Brussels Warehouse",
            parameter == "reference" ? value! : "November cycle count",
            CreatedAt,
            Guid.NewGuid()));
    }

    [Theory]
    [InlineData("default")]
    [InlineData("positiveOffset")]
    [InlineData("negativeOffset")]
    public void Constructor_WithInvalidTimestamp_Rejects(string scenario)
    {
        var timestamp = scenario switch
        {
            "default" => default,
            "positiveOffset" => CreatedAt.ToOffset(TimeSpan.FromHours(2)),
            _ => CreatedAt.ToOffset(TimeSpan.FromHours(-3))
        };

        Assert.Throws<ArgumentException>("createdAt", () => new StockCount(
            Guid.NewGuid(), "DEMO-BRU", "Brussels Warehouse", "Cycle count",
            timestamp, Guid.NewGuid()));
    }

    [Fact]
    public void Lines_CannotBeModifiedThroughAnExternalCollectionReference()
    {
        var count = CreateCount(Guid.NewGuid(), Guid.NewGuid());
        var collection = Assert.IsAssignableFrom<ICollection<StockCountLine>>(count.Lines);

        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Clear());
        Assert.Empty(count.Lines);
        Assert.Equal(StockCountStatus.Draft, count.Status);
    }

    private static StockCount CreateCount(Guid warehouseId, Guid creatorId) =>
        new(warehouseId, "DEMO-BRU", "Brussels Warehouse", "Cycle count",
            CreatedAt, creatorId);
}
