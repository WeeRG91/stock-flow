using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Domain.Entities;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Tests.Fakes;

internal sealed class FakeTransferReceiptRepository
    : ITransferReceiptRepository
{
    public TransferReceipt? ExistingReceipt { get; set; }
    public int LookupCallCount { get; private set; }
    public int SaveCallCount { get; private set; }
    public Exception? SaveFailure { get; set; }

    public CancellationToken LookupToken { get; private set; }
    public CancellationToken SaveToken { get; private set; }

    public TransferReceipt? LastSavedReceipt { get; private set; }
    public Transfer? LastSavedTransfer { get; private set; }

    public IReadOnlyList<InventoryEntity> LastSavedInventories
    { get; private set; } = [];

    public Task<TransferReceipt?> GetByIdAsync(
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        LookupCallCount++;
        LookupToken = cancellationToken;

        var receipt = ExistingReceipt;

        return Task.FromResult(
            receipt?.Id == receiptId ? receipt : null);
    }

    public Task SaveConfirmationAsync(
        TransferReceipt receipt,
        Transfer transfer,
        IReadOnlyList<InventoryEntity> destinationInventories,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SaveCallCount++;
        SaveToken = cancellationToken;

        if (SaveFailure is { } failure)
        {
            return Task.FromException(failure);
        }

        LastSavedReceipt = receipt;
        LastSavedTransfer = transfer;
        LastSavedInventories = destinationInventories.ToArray();

        return Task.CompletedTask;
    }
}