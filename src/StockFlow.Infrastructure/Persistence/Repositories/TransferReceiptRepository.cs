using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Persistence.Repositories
{
    public sealed class TransferReceiptRepository : ITransferReceiptRepository
    {
        private readonly StockFlowDbContext _context;
        private bool _saveFailed;

        public TransferReceiptRepository(StockFlowDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        public Task<TransferReceipt?> GetByIdAsync(
            Guid receiptId,
            CancellationToken cancellationToken = default)
        {
            if (_saveFailed)
            {
                throw new InvalidOperationException(
                    "A previous save failed. Discard this scope and its DbContext.");
            }

            return _context.TransferReceipts
                .AsNoTracking()
                .AsSingleQuery()
                .Include(receipt => receipt.Lines)
                .SingleOrDefaultAsync(
                    receipt => receipt.Id == receiptId,
                    cancellationToken);
        }

        public async Task SaveConfirmationAsync(
            TransferReceipt receipt,
            Transfer transfer,
            IReadOnlyList<InventoryEntity> destinationInventories,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(receipt);
            ArgumentNullException.ThrowIfNull(transfer);
            ArgumentNullException.ThrowIfNull(destinationInventories);
            cancellationToken.ThrowIfCancellationRequested();

            if (_saveFailed)
            {
                throw new InvalidOperationException(
                    "A previous save failed. Discard this scope and its DbContext.");
            }

            if (_context.Database.CurrentTransaction is not null ||
                System.Transactions.Transaction.Current is not null)
            {
                throw new InvalidOperationException(
                    "The receipt save must own its commit boundary.");
            }

            var transferEntry = _context.Entry(transfer);

            if (transferEntry.State is not
                (EntityState.Unchanged or EntityState.Modified))
            {
                throw new InvalidOperationException(
                    "Receiving requires a transfer tracked by this DbContext.");
            }

            var originalStatus = transferEntry
                .Property(item => item.Status)
                .OriginalValue;

            if (originalStatus is not
                (TransferStatus.Dispatched or TransferStatus.PartiallyReceived) ||
                transfer.Status is not
                (TransferStatus.PartiallyReceived or TransferStatus.Received))
            {
                throw new InvalidOperationException(
                    "Receiving requires a valid tracked transfer receipt transition.");
            }

            if (receipt.TransferId != transfer.Id ||
                !transfer.PendingReceipts.Any(
                    pending => ReferenceEquals(pending, receipt)))
            {
                throw new InvalidOperationException(
                    "The receipt must be applied to this transfer before saving.");
            }

            var transferLines = transfer.Lines.ToDictionary(line => line.Id);

            var remainingProductIds = receipt.Lines
                .Select(line => transferLines[line.TransferLineId].ProductId)
                .ToHashSet();

            if (destinationInventories.Count != remainingProductIds.Count)
            {
                throw new InvalidOperationException(
                    "Receiving requires one destination inventory per received product.");
            }

            foreach (var inventory in destinationInventories)
            {
                if (inventory is null)
                {
                    throw new InvalidOperationException(
                        "A destination inventory must not be null.");
                }

                if (inventory.WarehouseId != transfer.DestinationWarehouseId ||
                    !remainingProductIds.Remove(inventory.ProductId))
                {
                    throw new InvalidOperationException(
                        "Destination inventories must match the received products and destination warehouse.");
                }

                var state = _context.Entry(inventory).State;

                if (state is not
                    (EntityState.Detached or EntityState.Unchanged or EntityState.Modified))
                {
                    throw new InvalidOperationException(
                        "A destination inventory is in an unexpected tracking state.");
                }
            }

            var changes = destinationInventories
                .Select(inventory => (
                    Inventory: inventory,
                    Movements: inventory.PendingMovements.ToArray()))
                .ToArray();

            try
            {
                if (_context.Entry(receipt).State != EntityState.Detached)
                {
                    throw new InvalidOperationException(
                        "A new receipt must not already be tracked.");
                }

                transferEntry.Property<Guid>("Version").CurrentValue = Guid.NewGuid();

                _context.TransferReceipts.Add(receipt);

                foreach (var (inventory, movements) in changes)
                {
                    if (_context.Entry(inventory).State == EntityState.Detached)
                    {
                        _context.Inventories.Add(inventory);
                    }

                    foreach (var movement in movements)
                    {
                        if (_context.Entry(movement).State != EntityState.Detached)
                        {
                            throw new InvalidOperationException(
                                "A pending movement is already tracked. Discard this scope.");
                        }

                        _context.StockMovements.Add(movement);
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _saveFailed = true;

                throw new TransferConflictException(
                    transfer.Id,
                    "The transfer or destination inventory changed during receiving. " +
                    "Reload it and try again.");
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "PK_TransferReceipts"
                        or "IX_Inventories_ProductId_WarehouseId"
                })
            {
                _saveFailed = true;

                throw new TransferConflictException(
                    transfer.Id,
                    "Another operation used this receipt ID or created the destination balance. " +
                    "Reload before retrying the confirmation.");
            }
            catch
            {
                _saveFailed = true;
                throw;
            }

            foreach (var (inventory, movements) in changes)
            {
                inventory.AcknowledgePersistedMovements(
                    movements.Select(movement => movement.Id).ToArray());
            }

            transfer.AcknowledgePersistedReceipts([receipt.Id]);
        }
    }
}
