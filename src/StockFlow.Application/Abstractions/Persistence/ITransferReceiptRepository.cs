using StockFlow.Domain.Entities;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Abstractions.Persistence
{
    public interface ITransferReceiptRepository
    {
        Task<TransferReceipt?> GetByIdAsync(
            Guid receiptId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically commits the receipt, tracked transfer changes,
        /// destination balances, and their pending stock movements.
        /// </summary>
        Task SaveConfirmationAsync(
            TransferReceipt receipt,
            Transfer transfer,
            IReadOnlyList<InventoryEntity> destinationInventories,
            CancellationToken cancellationToken = default);
    }
}
