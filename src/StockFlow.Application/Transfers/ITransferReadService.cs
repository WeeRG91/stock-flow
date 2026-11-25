using StockFlow.Application.Transfers.GetTransferReceipts;
using StockFlow.Application.Transfers.GetTransfers;
using StockFlow.Domain.Enums;

namespace StockFlow.Application.Transfers
{
    public interface ITransferReadService
    {
        Task<TransferDetailsDto?> GetByIdAsync(
            Guid transferId,
            CancellationToken cancellationToken = default);

        Task<GetTransfersResult> GetPageAsync(
            int pageNumber,
            int pageSize,
            TransferStatus? status,
            string? search,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<TransferReceiptHistoryItemDto>?> GetReceiptsAsync(
            Guid transferId,
            CancellationToken cancellationToken = default);
    }
}
