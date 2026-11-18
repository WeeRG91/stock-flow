using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.Domain.Tests;

public sealed class PurchasingNotificationTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WithValidInput_CreatesUnreadNotification()
    {
        var recipientId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var notification = new PurchasingNotification(
            recipientId, orderId, PurchasingNotificationKind.PurchaseOrderSubmitted, CreatedAt);

        Assert.NotEqual(Guid.Empty, notification.Id);
        Assert.Equal(recipientId, notification.RecipientUserId);
        Assert.Equal(orderId, notification.PurchaseOrderId);
        Assert.Equal(PurchasingNotificationKind.PurchaseOrderSubmitted, notification.Kind);
        Assert.Equal(CreatedAt, notification.CreatedAt);
        Assert.Null(notification.ReadAt);
    }

    [Theory]
    [InlineData(true, "recipientUserId")]
    [InlineData(false, "purchaseOrderId")]
    public void Constructor_WithEmptyIdentifier_RejectsThatArgument(
        bool emptyRecipient, string expectedParameter)
    {
        var recipientId = emptyRecipient ? Guid.Empty : Guid.NewGuid();
        var orderId = emptyRecipient ? Guid.NewGuid() : Guid.Empty;

        Assert.Throws<ArgumentException>(
            expectedParameter,
            () => new PurchasingNotification(
                recipientId, orderId, PurchasingNotificationKind.PurchaseOrderSubmitted, CreatedAt));
    }

    [Fact]
    public void Constructor_WithUndefinedKind_RejectsIt()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "kind",
            () => new PurchasingNotification(
                Guid.NewGuid(), Guid.NewGuid(), (PurchasingNotificationKind)99, CreatedAt));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WithDefaultOrNonUtcCreationTime_RejectsIt(bool useDefault)
    {
        var createdAt = useDefault ? default : CreatedAt.ToOffset(TimeSpan.FromHours(1));

        Assert.Throws<ArgumentException>(
            "createdAt",
            () => new PurchasingNotification(
                Guid.NewGuid(), Guid.NewGuid(),
                PurchasingNotificationKind.PurchaseOrderApproved, createdAt));
    }

    [Fact]
    public void MarkAsRead_WhenCalledTwice_PreservesFirstReadTime()
    {
        var notification = CreateNotification();
        var firstReadAt = CreatedAt.AddMinutes(1);

        notification.MarkAsRead(firstReadAt);
        Assert.Equal(firstReadAt, notification.ReadAt);

        notification.MarkAsRead(firstReadAt.AddMinutes(1));
        Assert.Equal(firstReadAt, notification.ReadAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MarkAsRead_WithDefaultOrNonUtcTime_LeavesNotificationUnread(bool useDefault)
    {
        var notification = CreateNotification();
        var readAt = useDefault ? default : CreatedAt.ToOffset(TimeSpan.FromHours(1));

        Assert.Throws<ArgumentException>("readAt", () => notification.MarkAsRead(readAt));
        Assert.Null(notification.ReadAt);
    }

    [Fact]
    public void MarkAsRead_BeforeCreation_LeavesNotificationUnread()
    {
        var notification = CreateNotification();

        Assert.Throws<ArgumentOutOfRangeException>(
            "readAt", () => notification.MarkAsRead(CreatedAt.AddSeconds(-1)));

        Assert.Null(notification.ReadAt);
    }

    private static PurchasingNotification CreateNotification()
    {
        return new PurchasingNotification(
            Guid.NewGuid(), Guid.NewGuid(), PurchasingNotificationKind.PurchaseOrderApproved, CreatedAt);
    }
}