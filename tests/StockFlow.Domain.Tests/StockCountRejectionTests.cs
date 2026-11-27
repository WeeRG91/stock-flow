using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.Domain.Tests;

public sealed class StockCountRejectionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Reject_WithIndependentReviewer_RecordsDecisionAndPreservesObservations(int delayMinutes)
    {
        var f = new Fixture();
        var reviewer = Guid.NewGuid();
        var reviewedAt = f.SubmittedAt.AddMinutes(delayMinutes);

        f.Count.Reject(reviewedAt, reviewer, "  Discrepancy reviewed.  ");

        Assert.Equal(StockCountStatus.Rejected, f.Count.Status);
        Assert.Equal(reviewedAt, f.Count.ReviewedAt);
        Assert.Equal(reviewer, f.Count.ReviewedByUserId);
        Assert.Equal("Discrepancy reviewed.", f.Count.ReviewReason);
        f.AssertOriginalObservationsAndSubmission();
    }

    [Fact]
    public void Reject_WithZeroVariance_RecordsReviewWithoutChangingTheObservation()
    {
        var f = new Fixture(counted: 100);

        f.Count.Reject(f.SubmittedAt, Guid.NewGuid(), "Independent recount required despite matching quantities.");

        Assert.Equal(StockCountStatus.Rejected, f.Count.Status);
        Assert.All(f.Count.Lines, line => Assert.Equal(0, line.Variance));
        f.AssertOriginalObservationsAndSubmission();
    }

    [Fact]
    public void Reject_ByCreatorWhoNeitherCountedNorSubmitted_IsAllowed()
    {
        var f = new Fixture();

        f.Count.Reject(f.SubmittedAt, f.CreatorId, "Independent review completed.");

        Assert.Equal(StockCountStatus.Rejected, f.Count.Status);
        Assert.Equal(f.CreatorId, f.Count.ReviewedByUserId);
        f.AssertOriginalObservationsAndSubmission();
    }

    [Fact]
    public void Reject_WhenDraft_RejectsWithoutChangingTheSession()
    {
        var f = new Fixture(submit: false);

        Assert.Throws<InvalidOperationException>(() =>
            f.Count.Reject(f.SubmittedAt, Guid.NewGuid(), "Reviewed."));

        Assert.Equal(StockCountStatus.Draft, f.Count.Status);
        Assert.Null(f.Count.SubmittedAt);
        Assert.Null(f.Count.SubmittedByUserId);
        Assert.Null(f.Count.ReviewedAt);
        Assert.Null(f.Count.ReviewedByUserId);
        Assert.Null(f.Count.ReviewReason);
        Assert.Equal(f.OriginalLines, f.Count.Lines.ToArray());
    }

    [Theory]
    [InlineData("default")]
    [InlineData("positiveOffset")]
    [InlineData("negativeOffset")]
    [InlineData("beforeSubmission")]
    public void Reject_WithInvalidTimestamp_PreservesSubmittedSession(string scenario)
    {
        var f = new Fixture();
        var timestamp = scenario switch
        {
            "default" => default,
            "positiveOffset" => f.SubmittedAt.ToOffset(TimeSpan.FromHours(2)),
            "negativeOffset" => f.SubmittedAt.ToOffset(TimeSpan.FromHours(-3)),
            _ => f.SubmittedAt.AddTicks(-1)
        };

        var error = Assert.ThrowsAny<ArgumentException>(() =>
            f.Count.Reject(timestamp, Guid.NewGuid(), "Reviewed."));

        Assert.Equal("reviewedAt", error.ParamName);
        f.AssertStillSubmitted();
    }

    [Fact]
    public void Reject_WithEmptyReviewer_PreservesSubmittedSession()
    {
        var f = new Fixture();

        Assert.Throws<ArgumentException>("reviewedByUserId", () =>
            f.Count.Reject(f.SubmittedAt, Guid.Empty, "Reviewed."));

        f.AssertStillSubmitted();
    }

    [Theory]
    [InlineData("submitter")]
    [InlineData("firstCounter")]
    [InlineData("secondCounter")]
    public void Reject_BySubmitterOrAnyCountingEmployee_Rejects(string actor)
    {
        var f = new Fixture();
        var reviewer = actor switch
        {
            "submitter" => f.SubmitterId,
            "firstCounter" => f.FirstCounterId,
            _ => f.SecondCounterId
        };

        Assert.Throws<InvalidOperationException>(() =>
            f.Count.Reject(f.SubmittedAt, reviewer, "Reviewed."));

        f.AssertStillSubmitted();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public void Reject_WithMissingReason_PreservesSubmittedSession(string? reason)
    {
        var f = new Fixture();

        Assert.Throws<ArgumentException>("reason", () =>
            f.Count.Reject(f.SubmittedAt, Guid.NewGuid(), reason!));

        f.AssertStillSubmitted();
    }

    [Fact]
    public void Reject_WhenRepeated_PreservesOriginalDecision()
    {
        var f = new Fixture();
        var reviewer = Guid.NewGuid();
        f.Count.Reject(f.SubmittedAt, reviewer, "Original review.");

        Assert.Throws<InvalidOperationException>(() =>
            f.Count.Reject(f.SubmittedAt.AddMinutes(5), Guid.NewGuid(), "Replacement review."));

        Assert.Equal(StockCountStatus.Rejected, f.Count.Status);
        Assert.Equal(f.SubmittedAt, f.Count.ReviewedAt);
        Assert.Equal(reviewer, f.Count.ReviewedByUserId);
        Assert.Equal("Original review.", f.Count.ReviewReason);
        f.AssertOriginalObservationsAndSubmission();
    }

    [Fact]
    public void Reject_AfterFailedSelfReview_AllowsIndependentReview()
    {
        var f = new Fixture();
        Assert.Throws<InvalidOperationException>(() =>
            f.Count.Reject(f.SubmittedAt, f.FirstCounterId, "Self review."));
        var reviewer = Guid.NewGuid();

        f.Count.Reject(f.SubmittedAt, reviewer, "Independent review.");

        Assert.Equal(StockCountStatus.Rejected, f.Count.Status);
        Assert.Equal(reviewer, f.Count.ReviewedByUserId);
        Assert.Equal("Independent review.", f.Count.ReviewReason);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("submit")]
    public void RejectedSession_RejectsDraftEditingAndResubmission(string operation)
    {
        var f = new Fixture();
        var reviewer = Guid.NewGuid();
        f.Count.Reject(f.SubmittedAt, reviewer, "Rejected observation.");

        Assert.Throws<InvalidOperationException>(() =>
        {
            switch (operation)
            {
                case "add":
                    f.Count.AddLine(Guid.NewGuid(), Guid.NewGuid(), "OTHER", "Other product",
                        1, 1, "version-2", f.SubmittedAt, Guid.NewGuid(), "New count.");
                    break;
                case "remove":
                    f.Count.RemoveLine(f.OriginalLines[0].Id);
                    break;
                default:
                    f.Count.Submit(f.SubmittedAt.AddMinutes(5), Guid.NewGuid());
                    break;
            }
        });

        Assert.Equal(StockCountStatus.Rejected, f.Count.Status);
        Assert.Equal(f.SubmittedAt, f.Count.ReviewedAt);
        Assert.Equal(reviewer, f.Count.ReviewedByUserId);
        Assert.Equal("Rejected observation.", f.Count.ReviewReason);
        f.AssertOriginalObservationsAndSubmission();
    }

    [Fact]
    public void Reject_AfterApproval_PreservesTheOriginalApproval()
    {
        var f = new Fixture();
        var reviewer = Guid.NewGuid();
        f.Count.Approve(f.SubmittedAt, reviewer, "Original approval.");

        Assert.Throws<InvalidOperationException>(() =>
            f.Count.Reject(f.SubmittedAt.AddMinutes(5), Guid.NewGuid(), "Later rejection."));

        Assert.Equal(StockCountStatus.Approved, f.Count.Status);
        Assert.Equal(f.SubmittedAt, f.Count.ReviewedAt);
        Assert.Equal(reviewer, f.Count.ReviewedByUserId);
        Assert.Equal("Original approval.", f.Count.ReviewReason);
        f.AssertOriginalObservationsAndSubmission();
    }

    [Fact]
    public void Approve_AfterRejection_PreservesTheOriginalRejection()
    {
        var f = new Fixture();
        var reviewer = Guid.NewGuid();
        f.Count.Reject(f.SubmittedAt, reviewer, "Recount required.");

        Assert.Throws<InvalidOperationException>(() =>
            f.Count.Approve(f.SubmittedAt.AddMinutes(5), Guid.NewGuid(), "Later approval."));

        Assert.Equal(StockCountStatus.Rejected, f.Count.Status);
        Assert.Equal(f.SubmittedAt, f.Count.ReviewedAt);
        Assert.Equal(reviewer, f.Count.ReviewedByUserId);
        Assert.Equal("Recount required.", f.Count.ReviewReason);
        f.AssertOriginalObservationsAndSubmission();
    }

    private sealed class Fixture
    {
        public DateTimeOffset CreatedAt { get; } = new(2026, 11, 27, 10, 0, 0, TimeSpan.Zero);
        public DateTimeOffset SubmittedAt => CreatedAt.AddMinutes(10);
        public Guid CreatorId { get; } = Guid.NewGuid();
        public Guid SubmitterId { get; } = Guid.NewGuid();
        public Guid FirstCounterId { get; } = Guid.NewGuid();
        public Guid SecondCounterId { get; } = Guid.NewGuid();
        public StockCount Count { get; }
        public StockCountLine[] OriginalLines { get; }

        public Fixture(bool submit = true, int counted = 97)
        {
            Count = new StockCount(Guid.NewGuid(), "DEMO-BRU", "Brussels Warehouse",
                "Cycle count", CreatedAt, CreatorId);
            Count.AddLine(Guid.NewGuid(), Guid.NewGuid(), "CHAIR", "Office Chair",
                100, counted, "first-version", CreatedAt.AddMinutes(5), FirstCounterId, "Shelf A checked.");
            Count.AddLine(Guid.NewGuid(), Guid.NewGuid(), "DESK", "Office Desk",
                100, counted, "second-version", CreatedAt.AddMinutes(7), SecondCounterId, "Shelf B checked.");
            OriginalLines = Count.Lines.ToArray();
            if (submit) Count.Submit(SubmittedAt, SubmitterId);
        }

        public void AssertStillSubmitted()
        {
            Assert.Equal(StockCountStatus.Submitted, Count.Status);
            Assert.Null(Count.ReviewedAt);
            Assert.Null(Count.ReviewedByUserId);
            Assert.Null(Count.ReviewReason);
            AssertOriginalObservationsAndSubmission();
        }

        public void AssertOriginalObservationsAndSubmission()
        {
            Assert.Equal(OriginalLines, Count.Lines.ToArray());
            Assert.Equal(SubmittedAt, Count.SubmittedAt);
            Assert.Equal(SubmitterId, Count.SubmittedByUserId);
            Assert.Equal(CreatedAt, Count.CreatedAt);
            Assert.Equal(CreatorId, Count.CreatedByUserId);
        }
    }
}
