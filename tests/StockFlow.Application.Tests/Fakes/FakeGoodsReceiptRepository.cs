using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Domain.Entities;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Tests.Fakes;

internal sealed class FakeGoodsReceiptRepository : IGoodsReceiptRepository
{
    public GoodsReceipt? ExistingReceipt { get; set; }
    public int LookupCallCount { get; private set; }
    public int SaveCallCount { get; private set; }
    public Exception? SaveFailure { get; set; }

    public GoodsReceipt? LastSavedReceipt { get; private set; }
    public PurchaseOrder? LastSavedOrder { get; private set; }
    public IReadOnlyList<InventoryEntity> LastSavedInventories { get; private set; }
        = Array.Empty<InventoryEntity>();

    public Task<GoodsReceipt?> GetByIdAsync(
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LookupCallCount++;

        var receipt = ExistingReceipt;
        return Task.FromResult(receipt?.Id == receiptId ? receipt : null);
    }

    public Task SaveConfirmationAsync(
        GoodsReceipt receipt,
        PurchaseOrder order,
        IReadOnlyList<InventoryEntity> inventories,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SaveCallCount++;

        if (SaveFailure is { } failure)
        {
            return Task.FromException(failure);
        }

        LastSavedReceipt = receipt;
        LastSavedOrder = order;
        LastSavedInventories = inventories.ToArray();
        return Task.CompletedTask;
    }
}