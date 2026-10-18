namespace StockFlow.Application.Inventory.MonitorLowStock
{
    public sealed record LowStockAlertSyncResult(
        int Created,
        int Reopened,
        int Resolved,
        int ActiveCount);
}
