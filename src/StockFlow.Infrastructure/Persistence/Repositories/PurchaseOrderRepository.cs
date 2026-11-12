using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;

namespace StockFlow.Infrastructure.Persistence.Repositories
{
    public sealed class PurchaseOrderRepository : IPurchaseOrderRepository
    {
        private readonly StockFlowDbContext _context;
        private bool _saveFailed;

        public PurchaseOrderRepository(StockFlowDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        public async Task<(PurchaseOrder Order, Guid Version)?> GetByIdAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default)
        {
            var order = await _context.PurchaseOrders
                .AsTracking()
                .AsSingleQuery()
                .Include(order => order.Lines)
                .SingleOrDefaultAsync(
                    order => order.Id == purchaseOrderId,
                    cancellationToken);

            if (order is null)
            {
                return null;
            }

            var version = _context.Entry(order)
                .Property<Guid>("Version")
                .OriginalValue;

            return (order, version);
        }

        public async Task SaveAsync(
            PurchaseOrder purchaseOrder,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(purchaseOrder);
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
                    "This save operation must own its commit boundary.");
            }

            try
            {
                await SaveCoreAsync(purchaseOrder, cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _saveFailed = true;
                throw new PurchaseOrderConflictException(
                    purchaseOrder.Id,
                    "The purchase order changed during this operation. Reload it and try again.");
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "IX_PurchaseOrders_OrderNumber"
                })
            {
                _saveFailed = true;
                throw new PurchaseOrderConflictException(
                    purchaseOrder.Id,
                    "The generated order number already exists. Retry creation.");
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "IX_PurchaseOrderLines_PurchaseOrderId_ProductId"
                })
            {
                _saveFailed = true;
                throw new PurchaseOrderConflictException(
                    purchaseOrder.Id,
                    "A product already has a line on this purchase order. Reload it and try again.");
            }
            catch
            {
                _saveFailed = true;
                throw;
            }
        }

        private async Task SaveCoreAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(purchaseOrder);
            cancellationToken.ThrowIfCancellationRequested();

            var tracker = _context.ChangeTracker;
            var previousAutoDetect = tracker.AutoDetectChangesEnabled;

            try
            {
                tracker.AutoDetectChangesEnabled = false;
                var entry = _context.Entry(purchaseOrder);

                if (entry.State == EntityState.Detached)
                {
                    entry = _context.PurchaseOrders.Add(purchaseOrder);
                }
                else if (entry.State is EntityState.Unchanged or EntityState.Modified)
                {
                    StageLineChanges(purchaseOrder);
                }
                else
                {
                    throw new InvalidOperationException(
                        "Cannot save a purchase order in its current tracking state.");
                }

                entry.Property<Guid>("Version").CurrentValue = Guid.NewGuid();
            }
            finally
            {
                tracker.AutoDetectChangesEnabled = previousAutoDetect;
            }

            tracker.DetectChanges();
            await _context.SaveChangesAsync(cancellationToken);
        }

        private void StageLineChanges(PurchaseOrder purchaseOrder)
        {
            var trackedLines = _context.ChangeTracker
                .Entries<PurchaseOrderLine>()
                .Where(entry =>
                    entry.Property<Guid>("PurchaseOrderId").CurrentValue == purchaseOrder.Id)
                .ToDictionary(entry => entry.Entity.Id);

            foreach (var line in purchaseOrder.Lines.ToArray())
            {
                if (!trackedLines.Remove(line.Id, out var previous))
                {
                    var added = _context.Entry(line);
                    added.Property<Guid>("PurchaseOrderId").CurrentValue = purchaseOrder.Id;
                    added.State = EntityState.Added;
                    continue;
                }

                if (ReferenceEquals(previous.Entity, line))
                {
                    continue;
                }

                var quantityChanged = previous.Entity.OrderedQuantity != line.OrderedQuantity;
                var priceChanged = previous.Entity.UnitPrice != line.UnitPrice;

                previous.State = EntityState.Detached;

                var replacement = _context.Entry(line);
                replacement.Property<Guid>("PurchaseOrderId").CurrentValue = purchaseOrder.Id;
                replacement.State = EntityState.Unchanged;
                replacement.Property(item => item.OrderedQuantity).IsModified = quantityChanged;
                replacement.Property(item => item.UnitPrice).IsModified = priceChanged;
            }

            foreach (var removed in trackedLines.Values)
            {
                removed.State = EntityState.Deleted;
            }
        }
    }
}
