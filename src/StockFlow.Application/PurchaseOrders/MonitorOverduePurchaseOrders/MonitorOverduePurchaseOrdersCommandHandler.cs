using StockFlow.Application.Abstractions.Time;

namespace StockFlow.Application.PurchaseOrders.MonitorOverduePurchaseOrders
{
    public sealed class MonitorOverduePurchaseOrdersCommandHandler(
        IOverduePurchaseOrderReader reader,
        IOverduePurchaseOrderAlertStore alerts,
        IClock clock)
    {
        private static readonly TimeZoneInfo LuxembourgTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Luxembourg");

        public async Task<OverduePurchaseOrderAlertSyncResult> HandleAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var evaluatedAt = clock.UtcNow;
            if (evaluatedAt == default || evaluatedAt.Offset != TimeSpan.Zero)
            {
                throw new InvalidOperationException(
                    "Overdue monitoring requires a non-default UTC timestamp.");
            }

            var localTime = TimeZoneInfo.ConvertTime(evaluatedAt, LuxembourgTimeZone);
            var today = DateOnly.FromDateTime(localTime.DateTime);

            var candidates = await reader.GetCandidatesAsync(today, cancellationToken);
            var overdueOrderIds = candidates
                .Where(order => order.IsOverdue(today))
                .Select(order => order.Id)
                .Distinct()
                .ToArray();

            return await alerts.SynchronizeAsync(
                overdueOrderIds, evaluatedAt, cancellationToken);
        }
    }
}
