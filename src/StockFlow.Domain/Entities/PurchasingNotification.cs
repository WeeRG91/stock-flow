using StockFlow.Domain.Enums;

namespace StockFlow.Domain.Entities
{
    public sealed class PurchasingNotification
    {
        public Guid Id { get; private set; }
        public Guid RecipientUserId { get; private set; }
        public Guid PurchaseOrderId { get; private set; }
        public PurchasingNotificationKind Kind { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? ReadAt { get; private set; }

        public PurchasingNotification(
            Guid recipientUserId,
            Guid purchaseOrderId,
            PurchasingNotificationKind kind,
            DateTimeOffset createdAt)
        {
            if (recipientUserId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Recipient user ID must not be empty.", nameof(recipientUserId));
            }

            if (purchaseOrderId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Purchase order ID must not be empty.", nameof(purchaseOrderId));
            }

            if (!Enum.IsDefined(kind))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(kind), "Select a valid purchasing notification kind.");
            }

            if (createdAt == default || createdAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Creation time must be a non-default UTC value.", nameof(createdAt));
            }

            Id = Guid.NewGuid();
            RecipientUserId = recipientUserId;
            PurchaseOrderId = purchaseOrderId;
            Kind = kind;
            CreatedAt = createdAt;
        }

        public void MarkAsRead(DateTimeOffset readAt)
        {
            if (readAt == default || readAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Read time must be a non-default UTC value.", nameof(readAt));
            }

            if (readAt < CreatedAt)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(readAt), "Read time cannot precede creation.");
            }

            ReadAt ??= readAt;
        }
    }
}
