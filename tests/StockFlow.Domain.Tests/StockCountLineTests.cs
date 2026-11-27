using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.Domain.Tests;

public sealed class StockCountLineTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 27, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AddLine_CapturesAndNormalizesObservationWithoutChangingSessionIdentity()
    {
        var count = CreateCount();
        var countId = count.Id;
        var inventoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var countedAt = CreatedAt.AddMinutes(5);

        var lineId = count.AddLine(
            inventoryId, productId, "  demo-chair  ", "  Office Chair  ",
            100, 97, " opaque-token== ", countedAt, actorId, "  Shelf checked twice.  ");

        var line = Assert.Single(count.Lines);
        Assert.NotEqual(Guid.Empty, lineId);
        Assert.Equal(lineId, line.Id);
        Assert.Equal(inventoryId, line.InventoryId);
        Assert.Equal(productId, line.ProductId);
        Assert.Equal("DEMO-CHAIR", line.ProductSku);
        Assert.Equal("Office Chair", line.ProductName);
        Assert.Equal(100, line.CapturedQuantityOnHand);
        Assert.Equal(97, line.CountedQuantity);
        Assert.Equal(-3, line.Variance);
        Assert.Equal(" opaque-token== ", line.CapturedInventoryVersion);
        Assert.Equal(countedAt, line.CountedAt);
        Assert.Equal(actorId, line.CountedByUserId);
        Assert.Equal("Shelf checked twice.", line.Notes);
        Assert.Equal(countId, count.Id);
        Assert.Equal(StockCountStatus.Draft, count.Status);
    }

    [Theory]
    [InlineData(100, 97, -3)]
    [InlineData(100, 105, 5)]
    [InlineData(100, 100, 0)]
    [InlineData(100, 0, -100)]
    [InlineData(0, 0, 0)]
    [InlineData(0, int.MaxValue, int.MaxValue)]
    [InlineData(int.MaxValue, 0, -int.MaxValue)]
    [InlineData(int.MaxValue, int.MaxValue, 0)]
    public void AddLine_WithNonnegativeQuantities_CalculatesVariance(int captured, int counted, int expected)
    {
        var count = CreateCount();

        AddLine(count, captured: captured, counted: counted);

        Assert.Equal(expected, Assert.Single(count.Lines).Variance);
    }

    [Theory]
    [InlineData(true, -1, "capturedQuantityOnHand")]
    [InlineData(true, int.MinValue, "capturedQuantityOnHand")]
    [InlineData(false, -1, "countedQuantity")]
    [InlineData(false, int.MinValue, "countedQuantity")]
    public void AddLine_WithNegativeQuantity_PreservesExistingObservation(bool captured, int value, string parameter)
    {
        var count = CreateCount();
        AddLine(count);
        var original = Assert.Single(count.Lines);

        Assert.Throws<ArgumentOutOfRangeException>(parameter, () => AddLine(
            count, captured: captured ? value : 100, counted: captured ? 97 : value));

        AssertUnchanged(count, original);
    }

    [Theory]
    [InlineData("productSku", null)]
    [InlineData("productSku", "")]
    [InlineData("productSku", " \t\r\n ")]
    [InlineData("productName", null)]
    [InlineData("productName", "")]
    [InlineData("productName", " \t\r\n ")]
    [InlineData("capturedInventoryVersion", null)]
    [InlineData("capturedInventoryVersion", "")]
    [InlineData("capturedInventoryVersion", " \t\r\n ")]
    [InlineData("notes", null)]
    [InlineData("notes", "")]
    [InlineData("notes", " \t\r\n ")]
    public void AddLine_WithMissingRequiredText_PreservesExistingObservation(string parameter, string? value)
    {
        var count = CreateCount();
        AddLine(count);
        var original = Assert.Single(count.Lines);

        Assert.Throws<ArgumentException>(parameter, () => count.AddLine(
            Guid.NewGuid(), Guid.NewGuid(),
            parameter == "productSku" ? value! : "DEMO-CHAIR",
            parameter == "productName" ? value! : "Office Chair",
            100, 97,
            parameter == "capturedInventoryVersion" ? value! : "version-1",
            CreatedAt, Guid.NewGuid(),
            parameter == "notes" ? value! : "Shelf checked."));

        AssertUnchanged(count, original);
    }

    [Theory]
    [InlineData("inventoryId")]
    [InlineData("productId")]
    [InlineData("countedByUserId")]
    public void AddLine_WithEmptyIdentifier_PreservesExistingObservation(string parameter)
    {
        var count = CreateCount();
        AddLine(count);
        var original = Assert.Single(count.Lines);

        Assert.Throws<ArgumentException>(parameter, () => count.AddLine(
            parameter == "inventoryId" ? Guid.Empty : Guid.NewGuid(),
            parameter == "productId" ? Guid.Empty : Guid.NewGuid(),
            "DEMO-CHAIR", "Office Chair", 100, 97, "version-1", CreatedAt,
            parameter == "countedByUserId" ? Guid.Empty : Guid.NewGuid(),
            "Shelf checked."));

        AssertUnchanged(count, original);
    }

    [Theory]
    [InlineData("default")]
    [InlineData("positiveOffset")]
    [InlineData("negativeOffset")]
    [InlineData("beforeCreation")]
    public void AddLine_WithInvalidTimestamp_PreservesExistingObservation(string scenario)
    {
        var count = CreateCount();
        AddLine(count);
        var original = Assert.Single(count.Lines);
        var timestamp = scenario switch
        {
            "default" => default,
            "positiveOffset" => CreatedAt.ToOffset(TimeSpan.FromHours(2)),
            "negativeOffset" => CreatedAt.ToOffset(TimeSpan.FromHours(-3)),
            _ => CreatedAt.AddTicks(-1)
        };

        var error = Assert.ThrowsAny<ArgumentException>(() => AddLine(count, countedAt: timestamp));

        Assert.Equal("countedAt", error.ParamName);
        AssertUnchanged(count, original);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddLine_WithDuplicateProductOrInventory_PreservesExistingObservation(bool duplicateProduct)
    {
        var count = CreateCount();
        AddLine(count);
        var original = Assert.Single(count.Lines);

        Assert.Throws<InvalidOperationException>(() => count.AddLine(
            duplicateProduct ? Guid.NewGuid() : original.InventoryId,
            duplicateProduct ? original.ProductId : Guid.NewGuid(),
            "OTHER-SKU", "Other product", 50, 40, "other-version", CreatedAt,
            Guid.NewGuid(), "Another observation."));

        AssertUnchanged(count, original);
    }

    [Fact]
    public void AddLine_WithDistinctProducts_PreservesEarlierObservationAndAssignsDistinctLineIds()
    {
        var count = CreateCount();
        var firstId = AddLine(count);
        var first = Assert.Single(count.Lines);

        var secondId = AddLine(count, captured: 20, counted: 21);

        Assert.Equal(2, count.Lines.Count);
        Assert.NotEqual(firstId, secondId);
        Assert.Same(first, count.Lines[0]);
        Assert.Equal(-3, first.Variance);
        Assert.Equal(1, count.Lines[1].Variance);
    }

    [Fact]
    public void Lines_WithAnObservation_RejectsExternalRemovalAndReplacement()
    {
        var count = CreateCount();
        AddLine(count);
        var original = Assert.Single(count.Lines);
        var collection = Assert.IsAssignableFrom<IList<StockCountLine>>(count.Lines);

        Assert.Throws<NotSupportedException>(() => collection.RemoveAt(0));
        Assert.Throws<NotSupportedException>(() => collection[0] = original);
        AssertUnchanged(count, original);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RemoveLine_RemovesOnlySelectedObservation(int position)
    {
        var count = CreateCount();
        AddLine(count);
        AddLine(count, captured: 20, counted: 21);
        AddLine(count, captured: 50, counted: 50);
        var original = count.Lines.ToArray();
        var retained = original.Where((_, index) => index != position).ToArray();

        count.RemoveLine(original[position].Id);

        Assert.Equal(2, count.Lines.Count);
        Assert.Same(retained[0], count.Lines[0]);
        Assert.Same(retained[1], count.Lines[1]);
        Assert.DoesNotContain(count.Lines, line => line.Id == original[position].Id);
        Assert.Equal(StockCountStatus.Draft, count.Status);
    }

    [Fact]
    public void RemoveLine_WithLastObservation_LeavesAnEmptyDraftAndUpdatesReadOnlyView()
    {
        var count = CreateCount();
        var id = AddLine(count);
        var view = count.Lines;

        count.RemoveLine(id);

        Assert.Empty(count.Lines);
        Assert.Empty(view);
        Assert.Equal(StockCountStatus.Draft, count.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RemoveLine_WithEmptyOrUnknownId_PreservesExistingObservation(bool empty)
    {
        var count = CreateCount();
        AddLine(count);
        var original = Assert.Single(count.Lines);

        if (empty)
        {
            Assert.Throws<ArgumentException>("lineId", () => count.RemoveLine(Guid.Empty));
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => count.RemoveLine(Guid.NewGuid()));
        }

        AssertUnchanged(count, original);
    }

    [Fact]
    public void RemoveLine_WithAnotherSessionsLine_RejectsWithoutChangingEitherSession()
    {
        var count = CreateCount();
        var other = CreateCount();
        AddLine(count);
        var foreignId = AddLine(other);
        var original = Assert.Single(count.Lines);
        var foreign = Assert.Single(other.Lines);

        Assert.Throws<InvalidOperationException>(() => count.RemoveLine(foreignId));

        AssertUnchanged(count, original);
        AssertUnchanged(other, foreign);
    }

    [Fact]
    public void RemoveLine_WhenRepeated_RejectsWithoutRemovingOtherObservations()
    {
        var count = CreateCount();
        var removedId = AddLine(count);
        AddLine(count);
        var retained = count.Lines[1];
        count.RemoveLine(removedId);

        Assert.Throws<InvalidOperationException>(() => count.RemoveLine(removedId));

        AssertUnchanged(count, retained);
    }

    [Fact]
    public void RemoveLine_AllowsANewObservationForTheSameProductWhileDraft()
    {
        var count = CreateCount();
        var removedId = AddLine(count);
        var removed = Assert.Single(count.Lines);
        count.RemoveLine(removedId);

        var replacementId = count.AddLine(
            removed.InventoryId, removed.ProductId, removed.ProductSku, removed.ProductName,
            100, 98, "new-version", CreatedAt.AddMinutes(10), Guid.NewGuid(), "Recounted shelf.");

        var replacement = Assert.Single(count.Lines);
        Assert.NotEqual(removedId, replacementId);
        Assert.Equal(removed.ProductId, replacement.ProductId);
        Assert.Equal(removed.InventoryId, replacement.InventoryId);
        Assert.Equal(-2, replacement.Variance);
        Assert.Equal(97, removed.CountedQuantity);
    }

    private static StockCount CreateCount() =>
        new(Guid.NewGuid(), "DEMO-BRU", "Brussels Warehouse", "Cycle count",
            CreatedAt, Guid.NewGuid());

    private static Guid AddLine(StockCount count, int captured = 100, int counted = 97,
        DateTimeOffset? countedAt = null) =>
        count.AddLine(Guid.NewGuid(), Guid.NewGuid(), "DEMO-CHAIR", "Office Chair",
            captured, counted, "version-1", countedAt ?? CreatedAt,
            Guid.NewGuid(), "Shelf checked.");

    private static void AssertUnchanged(StockCount count, StockCountLine original)
    {
        Assert.Same(original, Assert.Single(count.Lines));
        Assert.Equal(StockCountStatus.Draft, count.Status);
        Assert.Equal(100, original.CapturedQuantityOnHand);
        Assert.Equal(97, original.CountedQuantity);
        Assert.Equal(-3, original.Variance);
    }
}
