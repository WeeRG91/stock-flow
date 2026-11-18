using StockFlow.Domain.Entities;

namespace StockFlow.Domain.Tests;

public sealed class OverduePurchaseOrderAlertTests
{
    private static readonly DateTimeOffset DetectedAt =
        new(2026, 11, 18, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_CreatesActiveAlert()
    {
        var orderId = Guid.NewGuid();

        var alert = new OverduePurchaseOrderAlert(orderId, DetectedAt);

        Assert.NotEqual(Guid.Empty, alert.Id);
        Assert.Equal(orderId, alert.PurchaseOrderId);
        Assert.Equal(DetectedAt, alert.DetectedAt);
        Assert.Null(alert.ResolvedAt);
        Assert.True(alert.IsActive);
    }

    [Fact]
    public void Constructor_WithEmptyOrderId_RejectsIt()
    {
        Assert.Throws<ArgumentException>(
            "purchaseOrderId",
            () => new OverduePurchaseOrderAlert(Guid.Empty, DetectedAt));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WithDefaultOrNonUtcDetectionTime_RejectsIt(bool useDefault)
    {
        var detectedAt = useDefault
            ? default
            : DetectedAt.ToOffset(TimeSpan.FromHours(1));

        Assert.Throws<ArgumentException>(
            "detectedAt",
            () => new OverduePurchaseOrderAlert(Guid.NewGuid(), detectedAt));
    }

    [Fact]
    public void Resolve_WhenCalledTwice_PreservesFirstResolutionTime()
    {
        var alert = new OverduePurchaseOrderAlert(Guid.NewGuid(), DetectedAt);
        var firstResolution = DetectedAt.AddHours(1);

        alert.Resolve(firstResolution);
        alert.Resolve(firstResolution.AddHours(1));

        Assert.False(alert.IsActive);
        Assert.Equal(firstResolution, alert.ResolvedAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Resolve_WithDefaultOrNonUtcTime_LeavesAlertActive(bool useDefault)
    {
        var alert = new OverduePurchaseOrderAlert(Guid.NewGuid(), DetectedAt);
        var resolvedAt = useDefault
            ? default
            : DetectedAt.AddHours(1).ToOffset(TimeSpan.FromHours(1));

        Assert.Throws<ArgumentException>("resolvedAt", () => alert.Resolve(resolvedAt));

        Assert.True(alert.IsActive);
        Assert.Null(alert.ResolvedAt);
    }

    [Fact]
    public void Resolve_BeforeDetection_LeavesAlertActive()
    {
        var alert = new OverduePurchaseOrderAlert(Guid.NewGuid(), DetectedAt);

        Assert.Throws<ArgumentException>(
            "resolvedAt", () => alert.Resolve(DetectedAt.AddSeconds(-1)));

        Assert.True(alert.IsActive);
        Assert.Null(alert.ResolvedAt);
    }
}