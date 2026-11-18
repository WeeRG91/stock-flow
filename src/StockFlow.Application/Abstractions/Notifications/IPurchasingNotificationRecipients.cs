namespace StockFlow.Application.Abstractions.Notifications
{
    public interface IPurchasingNotificationRecipients
    {
        Task<IReadOnlyList<Guid>> GetApproverUserIdsAsync(
            CancellationToken cancellationToken = default);
    }
}
