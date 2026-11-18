using StockFlow.Domain.Enums;

namespace StockFlow.Application.PurchasingNotifications.GetPurchasingNotifications
{
    public sealed record PurchasingNotificationDto(
        Guid Id,
        Guid PurchaseOrderId,
        PurchasingNotificationKind Kind,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ReadAt);

    public sealed record GetPurchasingNotificationsResult(
        IReadOnlyList<PurchasingNotificationDto> Items,
        int PageNumber,
        int PageSize,
        int TotalCount,
        int UnreadCount);
}
