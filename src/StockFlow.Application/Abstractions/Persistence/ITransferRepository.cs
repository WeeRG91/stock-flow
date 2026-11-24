using StockFlow.Domain.Entities;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Abstractions.Persistence
{
    public interface ITransferRepository
    {
        Task<(Transfer Transfer, Guid Version)?> GetByIdAsync(
            Guid transferId,
            CancellationToken cancellationToken = default);

        Task SaveDraftAsync(
            Transfer transfer,
            CancellationToken cancellationToken = default);

        Task SaveCancellationAsync(
            Transfer transfer,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically commits the tracked transfer dispatch, source inventory
        /// changes, and their pending stock movements.
        /// </summary>
        Task SaveDispatchAsync(
            Transfer transfer,
            IReadOnlyList<InventoryEntity> sourceInventories,
            CancellationToken cancellationToken = default);
    }
}
