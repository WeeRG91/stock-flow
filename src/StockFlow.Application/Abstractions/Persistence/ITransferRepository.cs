using StockFlow.Domain.Entities;

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
    }
}
