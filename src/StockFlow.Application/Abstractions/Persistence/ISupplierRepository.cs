using StockFlow.Domain.Entities;

namespace StockFlow.Application.Abstractions.Persistence
{
    public interface ISupplierRepository
    {
        /// <summary>
        /// Loads a supplier for modification in the current persistence scope.
        /// Returns null when the supplier does not exist.
        /// </summary>
        Task<Supplier?> GetByIdAsync(
            Guid supplierId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks for a normalized code across active and inactive suppliers.
        /// </summary>
        Task<bool> ExistsByCodeAsync(
            string normalizedCode,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Inserts a new supplier or saves changes to an existing
        /// supplier tracked by the current persistence scope.
        /// Completes persistence before returning successfully.
        /// </summary>
        Task SaveAsync(
            Supplier supplier,
            CancellationToken cancellationToken = default);
    }
}
