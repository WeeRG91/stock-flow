using StockFlow.Domain.Entities;

namespace StockFlow.Application.Abstractions.Persistence
{
    public interface IPurchaseOrderRepository
    {
        /// <summary>
        /// Loads a tracked purchase order with all its lines.
        /// Returns null when the order does not exist.
        /// </summary>
        Task<(PurchaseOrder Order, Guid Version)?> GetByIdAsync(
            Guid purchaseOrderId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Persists a new aggregate or changes to an aggregate loaded
        /// in this persistence scope, including its line changes.
        /// Completes one atomic commit before returning successfully.
        /// </summary>
        Task SaveAsync(
            PurchaseOrder purchaseOrder,
            CancellationToken cancellationToken = default);
    }
}
