using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Domain.Entities;

namespace StockFlow.Application.Tests.Fakes;

internal sealed class FakePurchaseOrderRepository : IPurchaseOrderRepository
{
    public PurchaseOrder? LastSavedOrder { get; private set; }
    public int SaveCallCount { get; private set; }
    public CancellationToken SaveToken { get; private set; }
    public (PurchaseOrder Order, Guid Version)? Loaded { get; set; }
    public int LookupCallCount { get; private set; }

    public Task<(PurchaseOrder Order, Guid Version)?> GetByIdAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LookupCallCount++;

        var result = Loaded is { } loaded && loaded.Order.Id == purchaseOrderId
            ? Loaded
            : null;

        return Task.FromResult(result);
    }

    public Task SaveAsync(
        PurchaseOrder purchaseOrder,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SaveCallCount++;
        LastSavedOrder = purchaseOrder;
        SaveToken = cancellationToken;

        return Task.CompletedTask;
    }
}
