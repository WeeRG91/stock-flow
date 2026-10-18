using Microsoft.EntityFrameworkCore;
using StockFlow.Application.Inventory.MonitorLowStock;
using StockFlow.Domain.Entities;
using StockFlow.Infrastructure.Persistence;

namespace StockFlow.Infrastructure.Inventory.MonitorLowStock
{
    public sealed class LowStockAlertStore : ILowStockAlertStore
    {
        private readonly StockFlowDbContext _context;
        private const long ReconciliationLockKey = 17_001;
        private bool _saveFailed;

        public LowStockAlertStore(StockFlowDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        public async Task<LowStockAlertSyncResult> SynchronizeAsync(
            IReadOnlyCollection<Guid> lowStockInventoryIds, 
            DateTimeOffset evaluatedAt, 
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(lowStockInventoryIds);
            cancellationToken.ThrowIfCancellationRequested();

            if (evaluatedAt == default ||
                evaluatedAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Evaluation timestamp must be a non-default UTC value.",
                    nameof(evaluatedAt));
            }

            if (lowStockInventoryIds.Contains(Guid.Empty))
            {
                throw new ArgumentException(
                    "Inventory identifiers must not be empty.",
                    nameof(lowStockInventoryIds));
            }

            if (_saveFailed)
            {
                throw new InvalidOperationException(
                    "A previous synchronization failed. Discard this scope.");
            }

            if (_context.Database.CurrentTransaction is not null ||
                System.Transactions.Transaction.Current is not null)
            {
                throw new InvalidOperationException(
                    "Alert synchronization must own its transaction.");
            }

            if (_context.ChangeTracker.Entries().Any())
            {
                throw new InvalidOperationException(
                    "Alert synchronization requires a DbContext with no tracked entities.");
            }

            var lowIds = lowStockInventoryIds.ToHashSet();

            try
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(cancellationToken);

                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock({ReconciliationLockKey})",
                    cancellationToken);

                var alerts = await _context.LowStockAlerts
                    .AsTracking()
                    .ToDictionaryAsync(
                        alert => alert.InventoryId,
                        cancellationToken);

                var created = 0;
                var reopened = 0;
                var resolved = 0;

                foreach (var inventoryId in lowIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!alerts.TryGetValue(inventoryId, out var alert))
                    {
                        alert = new LowStockAlert(inventoryId, evaluatedAt);

                        _context.LowStockAlerts.Add(alert);
                        alerts.Add(inventoryId, alert);
                        created++;
                    }
                    else if (!alert.IsActive)
                    {
                        alert.Reopen(evaluatedAt);
                        reopened++;
                    }
                }

                foreach (var alert in alerts.Values)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (alert.IsActive && !lowIds.Contains(alert.InventoryId))
                    {
                        alert.Resolve(evaluatedAt);
                        resolved++;
                    }
                }

                var result = new LowStockAlertSyncResult(
                    created,
                    reopened,
                    resolved,
                    alerts.Values.Count(alert => alert.IsActive));

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                foreach (var alert in alerts.Values)
                {
                    _context.Entry(alert).State = EntityState.Detached;
                }

                return result;
            }
            catch
            {
                _saveFailed = true;
                throw;
            }
        }
    }
}
