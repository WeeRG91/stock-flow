using Microsoft.EntityFrameworkCore;
using StockFlow.Application.Transfers;
using StockFlow.Application.Transfers.GetTransfers;
using StockFlow.Domain.Enums;
using StockFlow.Infrastructure.Persistence;

namespace StockFlow.Infrastructure.Transfers
{
    public sealed class TransferReadService :ITransferReadService
    {
        private readonly StockFlowDbContext _context;

        public TransferReadService(StockFlowDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        public Task<TransferDetailsDto?> GetByIdAsync(
            Guid transferId,
            CancellationToken cancellationToken = default)
        {
            return _context.Transfers
                .AsNoTracking()
                .AsSingleQuery()
                .Where(transfer => transfer.Id == transferId)
                .Select(transfer => new TransferDetailsDto(
                    transfer.Id,
                    transfer.SourceWarehouseId,
                    transfer.SourceWarehouseCode,
                    transfer.SourceWarehouseName,
                    transfer.DestinationWarehouseId,
                    transfer.DestinationWarehouseCode,
                    transfer.DestinationWarehouseName,
                    transfer.Status,
                    transfer.CreatedAt,
                    transfer.CreatedByUserId,
                    transfer.DispatchedAt,
                    transfer.DispatchedByUserId,
                    transfer.CancelledAt,
                    transfer.CancelledByUserId,
                    transfer.ReceivedAt,
                    EF.Property<Guid>(transfer, "Version"),
                    transfer.Lines
                        .OrderBy(line => line.ProductSku)
                        .ThenBy(line => line.Id)
                        .Select(line => new TransferLineDto(
                            line.Id,
                            line.ProductId,
                            line.ProductSku,
                            line.ProductName,
                            line.Quantity,
                            line.DispatchedQuantity,
                            line.ReceivedQuantity))
                        .ToList()))
                .SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<GetTransfersResult> GetPageAsync(
        int pageNumber,
        int pageSize,
        TransferStatus? status,
        string? search,
        CancellationToken cancellationToken = default)
        {
            var transfers = _context.Transfers.AsNoTracking();

            if (status is { } selectedStatus)
            {
                transfers = transfers.Where(
                    transfer => transfer.Status == selectedStatus);
            }

            if (search is not null)
            {
                var escapedSearch = search
                    .Replace("\\", "\\\\")
                    .Replace("%", "\\%")
                    .Replace("_", "\\_");

                var pattern = $"%{escapedSearch}%";

                transfers = transfers.Where(transfer =>
                    EF.Functions.ILike(
                        transfer.SourceWarehouseCode, pattern, "\\") ||
                    EF.Functions.ILike(
                        transfer.SourceWarehouseName, pattern, "\\") ||
                    EF.Functions.ILike(
                        transfer.DestinationWarehouseCode, pattern, "\\") ||
                    EF.Functions.ILike(
                        transfer.DestinationWarehouseName, pattern, "\\"));
            }

            var offset = checked((int)(((long)pageNumber - 1) * pageSize));

            var totalCount = await transfers.CountAsync(cancellationToken);

            var items = await transfers
                .OrderByDescending(transfer => transfer.CreatedAt)
                .ThenByDescending(transfer => transfer.Id)
                .Skip(offset)
                .Take(pageSize)
                .Select(transfer => new TransferSummaryDto(
                    transfer.Id,
                    transfer.SourceWarehouseCode,
                    transfer.SourceWarehouseName,
                    transfer.DestinationWarehouseCode,
                    transfer.DestinationWarehouseName,
                    transfer.Status,
                    transfer.CreatedAt,
                    transfer.Lines.Count))
                .ToListAsync(cancellationToken);

            return new GetTransfersResult(
                items, pageNumber, pageSize, totalCount);
        }
    }
}
