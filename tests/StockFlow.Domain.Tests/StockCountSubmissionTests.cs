using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.Domain.Tests;

public sealed class StockCountSubmissionTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 27, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Submit_RecordsActorAndTimeWithoutReplacingObservations(int delayMinutes)
    {
        var count = CreateCount();
        AddLine(count, CreatedAt);
        var original = Assert.Single(count.Lines);
        var actor = Guid.NewGuid();
        var submittedAt = CreatedAt.AddMinutes(delayMinutes);

        count.Submit(submittedAt, actor);

        Assert.Equal(StockCountStatus.Submitted, count.Status);
        Assert.Equal(submittedAt, count.SubmittedAt);
        Assert.Equal(actor, count.SubmittedByUserId);
        Assert.Same(original, Assert.Single(count.Lines));
        Assert.Equal(100, original.CapturedQuantityOnHand);
        Assert.Equal(97, original.CountedQuantity);
        Assert.Equal(-3, original.Variance);
        Assert.Equal("version-1", original.CapturedInventoryVersion);
    }

    [Fact]
    public void Submit_WithNoObservations_RejectsAndLeavesDraft()
    {
        var count = CreateCount();

        Assert.Throws<InvalidOperationException>(() => count.Submit(CreatedAt, Guid.NewGuid()));

        AssertDraft(count);
        Assert.Empty(count.Lines);
    }

    [Fact]
    public void Submit_AfterRemovingLastObservation_RejectsAndLeavesDraft()
    {
        var count = CreateCount();
        var lineId = AddLine(count, CreatedAt);
        count.RemoveLine(lineId);

        Assert.Throws<InvalidOperationException>(() => count.Submit(CreatedAt, Guid.NewGuid()));

        AssertDraft(count);
        Assert.Empty(count.Lines);
    }

    [Theory]
    [InlineData("default")]
    [InlineData("positiveOffset")]
    [InlineData("negativeOffset")]
    [InlineData("beforeCreation")]
    public void Submit_WithInvalidTimestamp_PreservesDraftAndObservation(string scenario)
    {
        var count = CreateCount();
        AddLine(count, CreatedAt);
        var original = Assert.Single(count.Lines);
        var timestamp = scenario switch
        {
            "default" => default,
            "positiveOffset" => CreatedAt.ToOffset(TimeSpan.FromHours(2)),
            "negativeOffset" => CreatedAt.ToOffset(TimeSpan.FromHours(-3)),
            _ => CreatedAt.AddTicks(-1)
        };

        var error = Assert.ThrowsAny<ArgumentException>(() => count.Submit(timestamp, Guid.NewGuid()));

        Assert.Equal("submittedAt", error.ParamName);
        AssertDraft(count);
        Assert.Same(original, Assert.Single(count.Lines));
    }

    [Fact]
    public void Submit_BeforeLatestObservation_RejectsEvenWhenOtherObservationPredatesSubmission()
    {
        var count = CreateCount();
        AddLine(count, CreatedAt);
        AddLine(count, CreatedAt.AddMinutes(10));
        var original = count.Lines.ToArray();

        Assert.Throws<ArgumentOutOfRangeException>("submittedAt", () =>
            count.Submit(CreatedAt.AddMinutes(5), Guid.NewGuid()));

        AssertDraft(count);
        Assert.Equal(original, count.Lines.ToArray());
    }

    [Fact]
    public void Submit_WithEmptyActor_PreservesDraftAndObservation()
    {
        var count = CreateCount();
        AddLine(count, CreatedAt);
        var original = Assert.Single(count.Lines);

        Assert.Throws<ArgumentException>("submittedByUserId", () => count.Submit(CreatedAt, Guid.Empty));

        AssertDraft(count);
        Assert.Same(original, Assert.Single(count.Lines));
    }

    [Fact]
    public void Submit_WhenRepeated_PreservesOriginalSubmissionAndObservation()
    {
        var count = CreateCount();
        AddLine(count, CreatedAt);
        var original = Assert.Single(count.Lines);
        var actor = Guid.NewGuid();
        count.Submit(CreatedAt, actor);

        Assert.Throws<InvalidOperationException>(() =>
            count.Submit(CreatedAt.AddMinutes(10), Guid.NewGuid()));

        Assert.Equal(StockCountStatus.Submitted, count.Status);
        Assert.Equal(CreatedAt, count.SubmittedAt);
        Assert.Equal(actor, count.SubmittedByUserId);
        Assert.Same(original, Assert.Single(count.Lines));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SubmittedObservations_CannotBeAddedOrRemoved(bool add)
    {
        var count = CreateCount();
        var lineId = AddLine(count, CreatedAt);
        var original = Assert.Single(count.Lines);
        var actor = Guid.NewGuid();
        count.Submit(CreatedAt, actor);

        if (add)
            Assert.Throws<InvalidOperationException>(() => AddLine(count, CreatedAt));
        else
            Assert.Throws<InvalidOperationException>(() => count.RemoveLine(lineId));

        Assert.Same(original, Assert.Single(count.Lines));
        Assert.Equal(StockCountStatus.Submitted, count.Status);
        Assert.Equal(CreatedAt, count.SubmittedAt);
        Assert.Equal(actor, count.SubmittedByUserId);
    }

    [Fact]
    public void Submit_WithZeroVariance_StillRequiresAndRecordsReviewSubmission()
    {
        var count = CreateCount();
        count.AddLine(Guid.NewGuid(), Guid.NewGuid(), "DEMO-CHAIR", "Office Chair",
            0, 0, "version-1", CreatedAt, Guid.NewGuid(), "Empty shelf verified.");

        count.Submit(CreatedAt, Guid.NewGuid());

        Assert.Equal(0, Assert.Single(count.Lines).Variance);
        Assert.Equal(StockCountStatus.Submitted, count.Status);
        Assert.NotNull(count.SubmittedAt);
    }

    [Fact]
    public void Submit_AfterRejectedAttempt_AllowsDraftCorrectionAndLaterValidSubmission()
    {
        var count = CreateCount();
        AddLine(count, CreatedAt.AddMinutes(10));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            count.Submit(CreatedAt.AddMinutes(5), Guid.NewGuid()));
        AddLine(count, CreatedAt.AddMinutes(15));
        var actor = Guid.NewGuid();

        count.Submit(CreatedAt.AddMinutes(15), actor);

        Assert.Equal(2, count.Lines.Count);
        Assert.Equal(StockCountStatus.Submitted, count.Status);
        Assert.Equal(CreatedAt.AddMinutes(15), count.SubmittedAt);
        Assert.Equal(actor, count.SubmittedByUserId);
    }

    private static StockCount CreateCount() =>
        new(Guid.NewGuid(), "DEMO-BRU", "Brussels Warehouse", "Cycle count",
            CreatedAt, Guid.NewGuid());

    private static Guid AddLine(StockCount count, DateTimeOffset countedAt) =>
        count.AddLine(Guid.NewGuid(), Guid.NewGuid(), "DEMO-CHAIR", "Office Chair",
            100, 97, "version-1", countedAt, Guid.NewGuid(), "Shelf checked.");

    private static void AssertDraft(StockCount count)
    {
        Assert.Equal(StockCountStatus.Draft, count.Status);
        Assert.Null(count.SubmittedAt);
        Assert.Null(count.SubmittedByUserId);
    }
}
