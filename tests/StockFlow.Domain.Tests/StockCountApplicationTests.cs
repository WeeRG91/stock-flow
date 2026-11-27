using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.Domain.Tests;

public sealed class StockCountApplicationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void MarkApplied_RecordsCompletionAndPreservesObservationAndReviewHistory(int delayMinutes)
    {
        var f = new Fixture();
        var actor = Guid.NewGuid();
        var appliedAt = f.ReviewedAt.AddMinutes(delayMinutes);

        f.Count.MarkApplied(appliedAt, actor);

        Assert.Equal(StockCountStatus.Applied, f.Count.Status);
        Assert.Equal(appliedAt, f.Count.AppliedAt);
        Assert.Equal(actor, f.Count.AppliedByUserId);
        f.AssertFrozenHistory();
    }

    [Theory]
    [InlineData(StockCountStatus.Draft)]
    [InlineData(StockCountStatus.Submitted)]
    [InlineData(StockCountStatus.Rejected)]
    public void MarkApplied_WithoutApproval_PreservesOriginalState(StockCountStatus status)
    {
        var f = new Fixture(status);

        Assert.Throws<InvalidOperationException>(() =>
            f.Count.MarkApplied(f.ReviewedAt, Guid.NewGuid()));

        Assert.Equal(status, f.Count.Status);
        Assert.Null(f.Count.AppliedAt);
        Assert.Null(f.Count.AppliedByUserId);
        f.AssertFrozenHistory();
    }

    [Theory]
    [InlineData("default")]
    [InlineData("positiveOffset")]
    [InlineData("negativeOffset")]
    [InlineData("beforeApproval")]
    public void MarkApplied_WithInvalidTimestamp_PreservesApproval(string scenario)
    {
        var f = new Fixture();
        var timestamp = scenario switch
        {
            "default" => default,
            "positiveOffset" => f.ReviewedAt.ToOffset(TimeSpan.FromHours(2)),
            "negativeOffset" => f.ReviewedAt.ToOffset(TimeSpan.FromHours(-3)),
            _ => f.ReviewedAt.AddTicks(-1)
        };

        var error = Assert.ThrowsAny<ArgumentException>(() =>
            f.Count.MarkApplied(timestamp, Guid.NewGuid()));

        Assert.Equal("appliedAt", error.ParamName);
        f.AssertStillApproved();
    }

    [Fact]
    public void MarkApplied_WithEmptyActor_PreservesApproval()
    {
        var f = new Fixture();

        Assert.Throws<ArgumentException>("appliedByUserId", () =>
            f.Count.MarkApplied(f.ReviewedAt, Guid.Empty));

        f.AssertStillApproved();
    }

    [Fact]
    public void MarkApplied_WhenRepeated_PreservesOriginalApplicationHistory()
    {
        var f = new Fixture();
        var actor = Guid.NewGuid();
        f.Count.MarkApplied(f.ReviewedAt, actor);

        Assert.Throws<InvalidOperationException>(() =>
            f.Count.MarkApplied(f.ReviewedAt.AddMinutes(5), Guid.NewGuid()));

        Assert.Equal(StockCountStatus.Applied, f.Count.Status);
        Assert.Equal(f.ReviewedAt, f.Count.AppliedAt);
        Assert.Equal(actor, f.Count.AppliedByUserId);
        f.AssertFrozenHistory();
    }

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("submit")]
    [InlineData("approve")]
    [InlineData("reject")]
    public void AppliedSession_RejectsEditingAndFurtherReview(string operation)
    {
        var f = new Fixture();
        var actor = Guid.NewGuid();
        f.Count.MarkApplied(f.ReviewedAt, actor);

        Assert.Throws<InvalidOperationException>(() =>
        {
            switch (operation)
            {
                case "add":
                    f.Count.AddLine(Guid.NewGuid(), Guid.NewGuid(), "OTHER", "Other product",
                        1, 1, "new-version", f.ReviewedAt, Guid.NewGuid(), "Another count.");
                    break;
                case "remove":
                    f.Count.RemoveLine(f.OriginalLine.Id);
                    break;
                case "submit":
                    f.Count.Submit(f.ReviewedAt, Guid.NewGuid());
                    break;
                case "approve":
                    f.Count.Approve(f.ReviewedAt, Guid.NewGuid(), "Later approval.");
                    break;
                default:
                    f.Count.Reject(f.ReviewedAt, Guid.NewGuid(), "Later rejection.");
                    break;
            }
        });

        Assert.Equal(StockCountStatus.Applied, f.Count.Status);
        Assert.Equal(f.ReviewedAt, f.Count.AppliedAt);
        Assert.Equal(actor, f.Count.AppliedByUserId);
        f.AssertFrozenHistory();
    }

    [Fact]
    public void MarkApplied_WithZeroVariance_CompletesWithoutChangingCountValues()
    {
        var f = new Fixture(counted: 100);

        f.Count.MarkApplied(f.ReviewedAt, Guid.NewGuid());

        Assert.Equal(StockCountStatus.Applied, f.Count.Status);
        Assert.Equal(0, Assert.Single(f.Count.Lines).Variance);
        f.AssertFrozenHistory();
    }

    [Fact]
    public void MarkApplied_AfterInvalidAttempt_AllowsValidCompletion()
    {
        var f = new Fixture();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            f.Count.MarkApplied(f.ReviewedAt.AddTicks(-1), Guid.NewGuid()));
        f.AssertStillApproved();
        var actor = Guid.NewGuid();

        f.Count.MarkApplied(f.ReviewedAt, actor);

        Assert.Equal(StockCountStatus.Applied, f.Count.Status);
        Assert.Equal(actor, f.Count.AppliedByUserId);
        f.AssertFrozenHistory();
    }

    private sealed class Fixture
    {
        private static readonly DateTimeOffset CreatedAt =
            new(2026, 11, 27, 10, 0, 0, TimeSpan.Zero);
        public DateTimeOffset ReviewedAt => CreatedAt.AddMinutes(20);
        public StockCount Count { get; }
        public StockCountLine OriginalLine { get; }
        private readonly Guid _creator = Guid.NewGuid();
        private readonly Guid _submitter = Guid.NewGuid();
        private readonly Guid _reviewer = Guid.NewGuid();
        private readonly StockCountStatus _initialStatus;

        public Fixture(StockCountStatus status = StockCountStatus.Approved, int counted = 97)
        {
            _initialStatus = status;
            Count = new StockCount(Guid.NewGuid(), "DEMO-BRU", "Brussels Warehouse",
                "Cycle count", CreatedAt, _creator);
            Count.AddLine(Guid.NewGuid(), Guid.NewGuid(), "CHAIR", "Office Chair",
                100, counted, "original-version", CreatedAt.AddMinutes(5), Guid.NewGuid(), "Shelf checked.");
            OriginalLine = Assert.Single(Count.Lines);
            if (status == StockCountStatus.Draft) return;
            Count.Submit(CreatedAt.AddMinutes(10), _submitter);
            if (status == StockCountStatus.Submitted) return;
            if (status == StockCountStatus.Rejected)
                Count.Reject(ReviewedAt, _reviewer, "Original review.");
            else
                Count.Approve(ReviewedAt, _reviewer, "Original review.");
        }

        public void AssertStillApproved()
        {
            Assert.Equal(StockCountStatus.Approved, Count.Status);
            Assert.Null(Count.AppliedAt);
            Assert.Null(Count.AppliedByUserId);
            AssertFrozenHistory();
        }

        public void AssertFrozenHistory()
        {
            Assert.Same(OriginalLine, Assert.Single(Count.Lines));
            Assert.Equal("original-version", OriginalLine.CapturedInventoryVersion);
            Assert.Equal(CreatedAt, Count.CreatedAt);
            Assert.Equal(_creator, Count.CreatedByUserId);
            if (_initialStatus == StockCountStatus.Draft)
            {
                Assert.Null(Count.SubmittedAt);
                Assert.Null(Count.SubmittedByUserId);
            }
            else
            {
                Assert.Equal(CreatedAt.AddMinutes(10), Count.SubmittedAt);
                Assert.Equal(_submitter, Count.SubmittedByUserId);
            }
            if (_initialStatus is StockCountStatus.Draft or StockCountStatus.Submitted)
            {
                Assert.Null(Count.ReviewedAt);
                Assert.Null(Count.ReviewedByUserId);
                Assert.Null(Count.ReviewReason);
            }
            else
            {
                Assert.Equal(ReviewedAt, Count.ReviewedAt);
                Assert.Equal(_reviewer, Count.ReviewedByUserId);
                Assert.Equal("Original review.", Count.ReviewReason);
            }
        }
    }
}
