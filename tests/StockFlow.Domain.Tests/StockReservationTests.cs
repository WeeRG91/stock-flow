using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.Domain.Tests;

public sealed class StockReservationTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 25, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WithValidArguments_CreatesActiveReservation()
    {
        var inventoryId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();

        var reservation = new StockReservation(
            inventoryId,
            30,
            "  Future warehouse operation  ",
            CreatedAt,
            creatorId);

        Assert.NotEqual(Guid.Empty, reservation.Id);
        Assert.Equal(inventoryId, reservation.InventoryId);
        Assert.Equal(30, reservation.Quantity);
        Assert.Equal("Future warehouse operation", reservation.Reference);
        Assert.Equal(CreatedAt, reservation.CreatedAt);
        Assert.Equal(creatorId, reservation.CreatedByUserId);
        Assert.Equal(StockReservationStatus.Active, reservation.Status);

        Assert.Null(reservation.ReleasedAt);
        Assert.Null(reservation.ReleasedByUserId);
        Assert.Null(reservation.FulfilledAt);
        Assert.Null(reservation.FulfilledByUserId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_WithNonPositiveQuantity_Throws(int quantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "quantity",
            () => new StockReservation(
                Guid.NewGuid(),
                quantity,
                "Future warehouse operation",
                CreatedAt,
                Guid.NewGuid()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Constructor_WithPositiveBoundaryQuantity_Accepts(int quantity)
    {
        var reservation = new StockReservation(
            Guid.NewGuid(),
            quantity,
            "Future warehouse operation",
            CreatedAt,
            Guid.NewGuid());

        Assert.Equal(quantity, reservation.Quantity);
        Assert.Equal(StockReservationStatus.Active, reservation.Status);
    }

    [Theory]
    [InlineData(true, "inventoryId")]
    [InlineData(false, "createdByUserId")]
    public void Constructor_WithEmptyIdentifier_Throws(
    bool emptyInventoryId,
    string expectedParameter)
    {
        Assert.Throws<ArgumentException>(
            expectedParameter,
            () => new StockReservation(
                emptyInventoryId ? Guid.Empty : Guid.NewGuid(),
                30,
                "Future warehouse operation",
                CreatedAt,
                emptyInventoryId ? Guid.NewGuid() : Guid.Empty));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Constructor_WithBlankReference_Throws(string? reference)
    {
        Assert.Throws<ArgumentException>(
            "reference",
            () => new StockReservation(
                Guid.NewGuid(),
                30,
                reference!,
                CreatedAt,
                Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_WithDefaultTimestamp_Throws()
    {
        Assert.Throws<ArgumentException>(
            "createdAt",
            () => new StockReservation(
                Guid.NewGuid(),
                30,
                "Future warehouse operation",
                default,
                Guid.NewGuid()));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-5)]
    public void Constructor_WithNonUtcTimestamp_Throws(int offsetHours)
    {
        var timestamp = CreatedAt.ToOffset(
            TimeSpan.FromHours(offsetHours));

        Assert.Throws<ArgumentException>(
            "createdAt",
            () => new StockReservation(
                Guid.NewGuid(),
                30,
                "Future warehouse operation",
                timestamp,
                Guid.NewGuid()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void Release_WithValidArguments_RecordsRelease(int minutesLater)
    {
        var inventoryId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();

        var reservation = new StockReservation(
            inventoryId,
            30,
            "Future warehouse operation",
            CreatedAt,
            creatorId);

        var reservationId = reservation.Id;
        var releasedAt = CreatedAt.AddMinutes(minutesLater);
        var actorId = Guid.NewGuid();

        reservation.Release(releasedAt, actorId);

        Assert.Equal(StockReservationStatus.Released, reservation.Status);
        Assert.Equal(releasedAt, reservation.ReleasedAt);
        Assert.Equal(actorId, reservation.ReleasedByUserId);

        Assert.Null(reservation.FulfilledAt);
        Assert.Null(reservation.FulfilledByUserId);

        Assert.Equal(reservationId, reservation.Id);
        Assert.Equal(inventoryId, reservation.InventoryId);
        Assert.Equal(30, reservation.Quantity);
        Assert.Equal("Future warehouse operation", reservation.Reference);
        Assert.Equal(CreatedAt, reservation.CreatedAt);
        Assert.Equal(creatorId, reservation.CreatedByUserId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void Fulfill_WithValidArguments_RecordsFulfillment(int minutesLater)
    {
        var inventoryId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();

        var reservation = new StockReservation(
            inventoryId,
            30,
            "Future warehouse operation",
            CreatedAt,
            creatorId);

        var reservationId = reservation.Id;
        var fulfilledAt = CreatedAt.AddMinutes(minutesLater);
        var actorId = Guid.NewGuid();

        reservation.Fulfill(fulfilledAt, actorId);

        Assert.Equal(StockReservationStatus.Fulfilled, reservation.Status);
        Assert.Equal(fulfilledAt, reservation.FulfilledAt);
        Assert.Equal(actorId, reservation.FulfilledByUserId);

        Assert.Null(reservation.ReleasedAt);
        Assert.Null(reservation.ReleasedByUserId);

        Assert.Equal(reservationId, reservation.Id);
        Assert.Equal(inventoryId, reservation.InventoryId);
        Assert.Equal(30, reservation.Quantity);
        Assert.Equal("Future warehouse operation", reservation.Reference);
        Assert.Equal(CreatedAt, reservation.CreatedAt);
        Assert.Equal(creatorId, reservation.CreatedByUserId);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void TerminalReservation_RejectsFurtherTransitions(
    bool releaseFirst,
    bool releaseAgain)
    {
        var reservation = new StockReservation(
            Guid.NewGuid(),
            30,
            "Future warehouse operation",
            CreatedAt,
            Guid.NewGuid());

        var completedAt = CreatedAt.AddMinutes(10);
        var firstActorId = Guid.NewGuid();

        if (releaseFirst)
        {
            reservation.Release(completedAt, firstActorId);
        }
        else
        {
            reservation.Fulfill(completedAt, firstActorId);
        }

        var originalStatus = reservation.Status;
        var originalReleasedAt = reservation.ReleasedAt;
        var originalReleasedBy = reservation.ReleasedByUserId;
        var originalFulfilledAt = reservation.FulfilledAt;
        var originalFulfilledBy = reservation.FulfilledByUserId;

        Assert.Throws<InvalidOperationException>(() =>
        {
            if (releaseAgain)
            {
                reservation.Release(
                    completedAt.AddMinutes(1),
                    Guid.NewGuid());
            }
            else
            {
                reservation.Fulfill(
                    completedAt.AddMinutes(1),
                    Guid.NewGuid());
            }
        });

        Assert.Equal(originalStatus, reservation.Status);
        Assert.Equal(originalReleasedAt, reservation.ReleasedAt);
        Assert.Equal(originalReleasedBy, reservation.ReleasedByUserId);
        Assert.Equal(originalFulfilledAt, reservation.FulfilledAt);
        Assert.Equal(originalFulfilledBy, reservation.FulfilledByUserId);
    }

    [Theory]
    [InlineData(true, "default")]
    [InlineData(true, "nonUtc")]
    [InlineData(true, "beforeCreation")]
    [InlineData(false, "default")]
    [InlineData(false, "nonUtc")]
    [InlineData(false, "beforeCreation")]
    public void Transition_WithInvalidTimestamp_PreservesState(
    bool release,
    string timestampCase)
    {
        var reservation = new StockReservation(
            Guid.NewGuid(),
            30,
            "Future warehouse operation",
            CreatedAt,
            Guid.NewGuid());

        var timestamp = timestampCase switch
        {
            "default" => default(DateTimeOffset),
            "nonUtc" => CreatedAt.ToOffset(TimeSpan.FromHours(2)),
            "beforeCreation" => CreatedAt.AddTicks(-1),
            _ => throw new InvalidOperationException("Unknown test case.")
        };

        Action transition = () =>
        {
            if (release)
            {
                reservation.Release(timestamp, Guid.NewGuid());
            }
            else
            {
                reservation.Fulfill(timestamp, Guid.NewGuid());
            }
        };

        var parameterName = release ? "releasedAt" : "fulfilledAt";

        if (timestampCase == "beforeCreation")
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                parameterName, transition);
        }
        else
        {
            Assert.Throws<ArgumentException>(
                parameterName, transition);
        }

        Assert.Equal(StockReservationStatus.Active, reservation.Status);
        Assert.Null(reservation.ReleasedAt);
        Assert.Null(reservation.ReleasedByUserId);
        Assert.Null(reservation.FulfilledAt);
        Assert.Null(reservation.FulfilledByUserId);
    }

    [Theory]
    [InlineData(true, "releasedByUserId")]
    [InlineData(false, "fulfilledByUserId")]
    public void Transition_WithEmptyActor_PreservesState(
    bool release,
    string expectedParameter)
    {
        var reservation = new StockReservation(
            Guid.NewGuid(),
            30,
            "Future warehouse operation",
            CreatedAt,
            Guid.NewGuid());

        Assert.Throws<ArgumentException>(expectedParameter, () =>
        {
            if (release)
            {
                reservation.Release(
                    CreatedAt.AddMinutes(10),
                    Guid.Empty);
            }
            else
            {
                reservation.Fulfill(
                    CreatedAt.AddMinutes(10),
                    Guid.Empty);
            }
        });

        Assert.Equal(StockReservationStatus.Active, reservation.Status);
        Assert.Null(reservation.ReleasedAt);
        Assert.Null(reservation.ReleasedByUserId);
        Assert.Null(reservation.FulfilledAt);
        Assert.Null(reservation.FulfilledByUserId);
    }

    [Fact]
    public void Properties_DoNotExposePublicSetters()
    {
        var properties = typeof(StockReservation).GetProperties();

        Assert.NotEmpty(properties);

        foreach (var property in properties)
        {
            Assert.False(
                property.SetMethod?.IsPublic == true,
                $"{property.Name} must not expose a public setter.");
        }
    }
}