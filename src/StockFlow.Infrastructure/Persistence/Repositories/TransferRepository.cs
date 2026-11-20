using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.Infrastructure.Persistence.Repositories
{
    public sealed class TransferRepository : ITransferRepository
    {
        private readonly StockFlowDbContext _context;
        private bool _saveFailed;

        public TransferRepository(StockFlowDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        public async Task<(Transfer Transfer, Guid Version)?> GetByIdAsync(
            Guid transferId,
            CancellationToken cancellationToken = default)
        {
            if (_saveFailed)
            {
                throw new InvalidOperationException(
                    "A previous save failed. Discard this scope and its DbContext.");
            }

            var transfer = await _context.Transfers
                .AsTracking()
                .AsSingleQuery()
                .Include(transfer => transfer.Lines)
                .SingleOrDefaultAsync(
                    transfer => transfer.Id == transferId,
                    cancellationToken);

            if (transfer is null)
            {
                return null;
            }

            var version = _context.Entry(transfer)
                .Property<Guid>("Version")
                .OriginalValue;

            return (transfer, version);
        }

        public async Task SaveDraftAsync(
            Transfer transfer,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(transfer);
            cancellationToken.ThrowIfCancellationRequested();

            if (_saveFailed)
            {
                throw new InvalidOperationException(
                    "A previous save failed. Discard this scope and its DbContext.");
            }

            if (transfer.Status != TransferStatus.Draft)
            {
                throw new InvalidOperationException(
                    "Only draft transfers can be saved through this method.");
            }

            if (_context.Database.CurrentTransaction is not null ||
                System.Transactions.Transaction.Current is not null)
            {
                throw new InvalidOperationException(
                    "The draft save must own its commit boundary.");
            }

            try
            {
                var tracker = _context.ChangeTracker;
                var previousAutoDetect = tracker.AutoDetectChangesEnabled;

                try
                {
                    tracker.AutoDetectChangesEnabled = false;
                    var entry = _context.Entry(transfer);

                    if (entry.State == EntityState.Detached)
                    {
                        entry = _context.Transfers.Add(transfer);
                    }
                    else if (entry.State is EntityState.Unchanged or EntityState.Modified)
                    {
                        StageDraftLineChanges(transfer);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            "The transfer is in an unexpected tracking state.");
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
            catch (DbUpdateConcurrencyException)
            {
                _saveFailed = true;

                throw new TransferConflictException(
                    transfer.Id,
                    "The transfer changed during this operation. Reload it and try again.");
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "IX_TransferLines_TransferId_ProductId"
                })
            {
                _saveFailed = true;

                throw new TransferConflictException(
                    transfer.Id,
                    "A product already has a line on this transfer. Reload it and try again.");
            }
            catch
            {
                _saveFailed = true;
                throw;
            }
        }

        public async Task SaveCancellationAsync(
            Transfer transfer,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(transfer);
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
                    "The cancellation save must own its commit boundary.");
            }

            var entry = _context.Entry(transfer);

            if (entry.State is not (EntityState.Unchanged or EntityState.Modified) ||
                entry.Property(item => item.Status).OriginalValue != TransferStatus.Draft ||
                transfer.Status != TransferStatus.Cancelled)
            {
                throw new InvalidOperationException(
                    "Cancellation requires a tracked Draft-to-Cancelled transition.");
            }

            try
            {
                entry.Property<Guid>("Version").CurrentValue = Guid.NewGuid();

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _saveFailed = true;

                throw new TransferConflictException(
                    transfer.Id,
                    "The transfer changed during cancellation. Reload it and try again.");
            }
            catch
            {
                _saveFailed = true;
                throw;
            }
        }

        private void StageDraftLineChanges(Transfer transfer)
        {
            var trackedLines = _context.ChangeTracker
                .Entries<TransferLine>()
                .Where(entry =>
                    entry.Property<Guid>("TransferId").CurrentValue == transfer.Id)
                .ToDictionary(entry => entry.Entity.Id);

            foreach (var line in transfer.Lines)
            {
                if (trackedLines.Remove(line.Id, out var tracked))
                {
                    if (!ReferenceEquals(tracked.Entity, line))
                    {
                        throw new InvalidOperationException(
                            "An existing transfer line must retain its tracked instance.");
                    }

                    continue;
                }

                var added = _context.Entry(line);
                added.Property<Guid>("TransferId").CurrentValue = transfer.Id;
                added.State = EntityState.Added;
            }

            foreach (var removed in trackedLines.Values)
            {
                removed.State = EntityState.Deleted;
            }
        }
    }
}
