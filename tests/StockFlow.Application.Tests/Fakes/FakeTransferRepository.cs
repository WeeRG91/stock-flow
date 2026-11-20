using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Domain.Entities;

namespace StockFlow.Application.Tests.Fakes;

internal sealed class FakeTransferRepository : ITransferRepository
{
    public int SaveCallCount { get; private set; }
    public int CancellationSaveCallCount { get; private set; }
    public Transfer? LastSavedTransfer { get; private set; }
    public CancellationToken SaveToken { get; private set; }
    public Exception? SaveFailure { get; set; }
    public (Transfer Transfer, Guid Version)? Loaded { get; set; }
    public int LookupCallCount { get; private set; }
    public CancellationToken LookupToken { get; private set; }

    public Task<(Transfer Transfer, Guid Version)?> GetByIdAsync(
        Guid transferId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        LookupCallCount++;
        LookupToken = cancellationToken;

        var loaded = Loaded;
        var result = loaded is { } entry && entry.Transfer.Id == transferId
            ? loaded
            : null;

        return Task.FromResult(result);
    }

    public Task SaveCancellationAsync(
        Transfer transfer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CancellationSaveCallCount++;
        SaveToken = cancellationToken;

        if (SaveFailure is { } failure)
        {
            return Task.FromException(failure);
        }

        LastSavedTransfer = transfer;
        return Task.CompletedTask;
    }

    public Task SaveDraftAsync(
        Transfer transfer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SaveCallCount++;
        SaveToken = cancellationToken;

        if (SaveFailure is { } failure)
        {
            return Task.FromException(failure);
        }

        LastSavedTransfer = transfer;
        return Task.CompletedTask;
    }
}
