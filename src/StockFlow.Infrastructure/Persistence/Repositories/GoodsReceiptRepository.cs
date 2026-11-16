using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Persistence.Repositories
{
    public sealed class GoodsReceiptRepository : IGoodsReceiptRepository
    {
        private readonly StockFlowDbContext _context;
        private bool _saveFailed;

        public GoodsReceiptRepository(StockFlowDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        public Task<GoodsReceipt?> GetByIdAsync(
            Guid receiptId,
            CancellationToken cancellationToken = default)
        {
            return _context.GoodsReceipts
               .AsNoTracking()
               .AsSingleQuery()
               .Include(receipt => receipt.Lines)
               .SingleOrDefaultAsync(
                   receipt => receipt.Id == receiptId,
                   cancellationToken);
        }

        public async Task SaveConfirmationAsync(
            GoodsReceipt receipt,
            PurchaseOrder order,
            IReadOnlyList<InventoryEntity> inventories,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_saveFailed)
            {
                throw new InvalidOperationException(
                    "A previous confirmation save failed. Discard this scope and its DbContext.");
            }

            if (_context.Database.CurrentTransaction is not null ||
                System.Transactions.Transaction.Current is not null)
            {
                throw new InvalidOperationException(
                    "The confirmation save must own its commit boundary.");
            }

            var orderEntry = _context.Entry(order);
            if (orderEntry.State is not (EntityState.Unchanged or EntityState.Modified))
            {
                throw new InvalidOperationException(
                    "Confirmation requires a purchase order tracked by this DbContext.");
            }

            var deliveries = inventories
                .Select(inventory => (
                    Inventory: inventory,
                    Movement: inventory.PendingMovements.ToArray()))
                .ToArray();

            try
            {
                orderEntry.Property<Guid>("Version").CurrentValue = Guid.NewGuid();
                _context.GoodsReceipts.Add(receipt);

                foreach (var (inventory, movements) in deliveries)
                {
                    var entry = _context.Entry(inventory);
                    if (entry.State == EntityState.Detached)
                    {
                        _context.Inventories.Add(inventory);
                    }
                    else if (entry.State is not (EntityState.Unchanged or EntityState.Modified))
                    {
                        throw new InvalidOperationException(
                            "An inventory balance is in an unexpected tracking state.");
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

                throw new PurchaseOrderConflictException(
                    order.Id,
                    "The purchase order or inventory changed during confirmation. Reload and try again.");
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "PK_GoodsReceipts" or "IX_Inventories_ProductId_WarehouseId"
                })
            {
                _saveFailed = true;

                throw new PurchaseOrderConflictException(
                    order.Id,
                    "Another confirmation used this receipt ID or created the inventory balance. " +
                    "Reload the order before trying again.");
            }
            catch
            {
                _saveFailed = true;
                throw;
            }

            foreach (var (inventory, movements) in deliveries)
            {
                inventory.AcknowledgePersistedMovements(
                    movements.Select(movement => movement.Id).ToArray());
            }
        }
    }
}
