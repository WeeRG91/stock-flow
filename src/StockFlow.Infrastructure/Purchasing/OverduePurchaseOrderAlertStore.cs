using Microsoft.EntityFrameworkCore;
using StockFlow.Application.PurchaseOrders.MonitorOverduePurchaseOrders;
using StockFlow.Domain.Entities;
using StockFlow.Infrastructure.Persistence;

namespace StockFlow.Infrastructure.Purchasing
{
    public sealed class OverduePurchaseOrderAlertStore(StockFlowDbContext context) : IOverduePurchaseOrderAlertStore
    {
        private const long ReconciliationLockKey = 17_002;

        public async Task<OverduePurchaseOrderAlertSyncResult> SynchronizeAsync(
            IReadOnlyCollection<Guid> overduePurchaseOrderIds,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken = default)
        {
            var overdueIds = overduePurchaseOrderIds.ToHashSet();
            var candicateIds = overdueIds.ToArray();

            await using var transaction =
                await context.Database.BeginTransactionAsync(cancellationToken);

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({ReconciliationLockKey})",
                cancellationToken);

            var existingAlerts = await context.OverduePurchaseOrderAlerts
                .AsTracking()
                .Where(alert =>
                    alert.ResolvedAt == null || candicateIds.Contains(alert.PurchaseOrderId))
                .ToListAsync(cancellationToken);

            var knownOrderIds = existingAlerts
                .Select(alert => alert.PurchaseOrderId)
                .ToHashSet();

            var resolved = 0;
            foreach (var alert in existingAlerts)
            {
                if (alert.IsActive &&
                    alert.DetectedAt <= evaluatedAt &&
                    !overdueIds.Contains(alert.PurchaseOrderId))
                {
                    alert.Resolve(evaluatedAt);
                    resolved++;
                }
            }

            var created = 0;
            foreach (var orderId in overdueIds)
            {
                if (knownOrderIds.Contains(orderId)) continue;

                context.OverduePurchaseOrderAlerts.Add(
                    new OverduePurchaseOrderAlert(orderId, evaluatedAt));
                created++;
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new OverduePurchaseOrderAlertSyncResult(
                created,
                resolved,
                existingAlerts.Count(alert => alert.IsActive) + created);
        }
    }
}
