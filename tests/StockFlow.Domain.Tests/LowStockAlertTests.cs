using StockFlow.Domain.Entities;

namespace StockFlow.Domain.Tests;

public class LowStockAlertTests
{
    private static readonly DateTimeOffset DetectedAt =
        new(2026, 10, 16, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_CreatesActiveAlert()
    {
        var inventoryId = Guid.NewGuid();

        var alert = new LowStockAlert(inventoryId, DetectedAt);

        Assert.NotEqual(Guid.Empty, alert.Id);
        Assert.Equal(inventoryId, alert.InventoryId);
        Assert.Equal(DetectedAt, alert.DetectedAt);
        Assert.Null(alert.ResolvedAt);
        Assert.True(alert.IsActive);
    }

    [Fact]
    public void Constructor_WithEmptyInventoryId_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new LowStockAlert(Guid.Empty, DetectedAt));

        Assert.Equal("inventoryId", exception.ParamName);
    }

    [Theory]
    [InlineData("constructor", true)]
    [InlineData("constructor", false)]
    [InlineData("resolve", true)]
    [InlineData("resolve", false)]
    [InlineData("reopen", true)]
    [InlineData("reopen", false)]
    public void InvalidTimestamp_IsRejectedWithoutChangingState(
        string operation,
        bool useDefault)
    {
        var invalid = useDefault
            ? default(DateTimeOffset)
            : DetectedAt.ToOffset(TimeSpan.FromHours(2));

        if (operation == "constructor")
        {
            Assert.Throws<ArgumentException>(
                () => new LowStockAlert(Guid.NewGuid(), invalid));
            return;
        }

        var alert = new LowStockAlert(Guid.NewGuid(), DetectedAt);

        if (operation == "reopen")
        {
            alert.Resolve(DetectedAt.AddHours(1));
        }

        var previousResolution = alert.ResolvedAt;

        Assert.Throws<ArgumentException>(() =>
        {
            if (operation == "resolve")
                alert.Resolve(invalid);
            else
                alert.Reopen(invalid);
        });

        Assert.Equal(DetectedAt, alert.DetectedAt);
        Assert.Equal(previousResolution, alert.ResolvedAt);
    }

    [Fact]
    public void ResolveThenReopen_PreservesIdentityAndUpdatesLifecycle()
    {
        var inventoryId = Guid.NewGuid();
        var alert = new LowStockAlert(inventoryId, DetectedAt);
        var alertId = alert.Id;
        var resolvedAt = DetectedAt.AddHours(1);
        var reopenedAt = DetectedAt.AddHours(2);

        alert.Resolve(resolvedAt);

        Assert.False(alert.IsActive);
        Assert.Equal(resolvedAt, alert.ResolvedAt);
        Assert.Equal(DetectedAt, alert.DetectedAt);

        alert.Reopen(reopenedAt);

        Assert.True(alert.IsActive);
        Assert.Null(alert.ResolvedAt);
        Assert.Equal(reopenedAt, alert.DetectedAt);
        Assert.Equal(alertId, alert.Id);
        Assert.Equal(inventoryId, alert.InventoryId);
    }

    [Fact]
    public void RepeatedOperations_PreserveOriginalTimestamps()
    {
        var alert = new LowStockAlert(Guid.NewGuid(), DetectedAt);

        alert.Reopen(DetectedAt.AddMinutes(1));
        Assert.Equal(DetectedAt, alert.DetectedAt);

        var resolvedAt = DetectedAt.AddHours(1);
        alert.Resolve(resolvedAt);
        alert.Resolve(resolvedAt.AddMinutes(1));

        Assert.Equal(resolvedAt, alert.ResolvedAt);
        Assert.Equal(DetectedAt, alert.DetectedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TransitionWithEarlierTimestamp_ThrowsAndPreservesState(
        bool reopening)
    {
        var alert = new LowStockAlert(Guid.NewGuid(), DetectedAt);

        if (reopening)
        {
            alert.Resolve(DetectedAt.AddHours(1));
        }

        var previousResolution = alert.ResolvedAt;

        var exception = Assert.Throws<ArgumentException>(() =>
        {
            if (reopening)
                alert.Reopen(DetectedAt);
            else
                alert.Resolve(DetectedAt.AddTicks(-1));
        });

        Assert.Equal(
            reopening ? "detectedAt" : "resolvedAt",
            exception.ParamName);
        Assert.Equal(DetectedAt, alert.DetectedAt);
        Assert.Equal(previousResolution, alert.ResolvedAt);
    }

    [Fact]
    public void TransitionsWithEqualTimestamps_AreAllowed()
    {
        var alert = new LowStockAlert(Guid.NewGuid(), DetectedAt);

        alert.Resolve(DetectedAt);
        Assert.False(alert.IsActive);

        alert.Reopen(DetectedAt);
        Assert.True(alert.IsActive);
        Assert.Equal(DetectedAt, alert.DetectedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidTimestamp_IsRejectedEvenForRepeatedOperation(
        bool resolving)
    {
        var alert = new LowStockAlert(Guid.NewGuid(), DetectedAt);

        if (resolving)
        {
            alert.Resolve(DetectedAt.AddHours(1));
        }

        var previousResolution = alert.ResolvedAt;

        Assert.Throws<ArgumentException>(() =>
        {
            if (resolving)
                alert.Resolve(default);
            else
                alert.Reopen(default);
        });

        Assert.Equal(DetectedAt, alert.DetectedAt);
        Assert.Equal(previousResolution, alert.ResolvedAt);
    }
}