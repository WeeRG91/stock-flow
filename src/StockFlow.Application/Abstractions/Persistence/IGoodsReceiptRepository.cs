using StockFlow.Domain.Entities;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Abstractions.Persistence
{
    public interface IGoodsReceiptRepository
    {
        Task<GoodsReceipt?> GetByIdAsync(
            Guid receiptId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically commits the receipt, tracked order changes, inventory
        /// balances, and their pending stock movements.
        /// </summary>
        Task SaveConfirmationAsync(
            GoodsReceipt receipt,
            PurchaseOrder order,
            IReadOnlyList<InventoryEntity> inventories,
            CancellationToken cancellationToken = default);
    }
}
