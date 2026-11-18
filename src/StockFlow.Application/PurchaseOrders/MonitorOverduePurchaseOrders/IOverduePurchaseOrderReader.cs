using StockFlow.Domain.Entities;

namespace StockFlow.Application.PurchaseOrders.MonitorOverduePurchaseOrders
{
    public interface IOverduePurchaseOrderReader
    {
        Task<IReadOnlyList<PurchaseOrder>> GetCandidatesAsync(
            DateOnly today,
            CancellationToken cancellationToken = default);
    }

    public interface IOverduePurchaseOrderAlertStore
    {
        Task<OverduePurchaseOrderAlertSyncResult> SynchronizeAsync(
            IReadOnlyCollection<Guid> overduePurchaseOrderIds,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken = default);
    }

    public sealed record OverduePurchaseOrderAlertSyncResult(
        int Created,
        int Resolved,
        int ActiveCount);
}
