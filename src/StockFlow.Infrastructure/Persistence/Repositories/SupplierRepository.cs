using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;

namespace StockFlow.Infrastructure.Persistence.Repositories
{
    public sealed class SupplierRepository : ISupplierRepository
    {
        private readonly StockFlowDbContext _context;
        private bool _saveFailed;

        public SupplierRepository(StockFlowDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        public Task<Supplier?> GetByIdAsync(Guid supplierId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_saveFailed)
            {
                throw new InvalidOperationException(
                    "A previous save failed. Discard this scope and its DbContext.");
            }

            return _context.Suppliers
                .AsTracking()
                .SingleOrDefaultAsync(
                    supplier => supplier.Id == supplierId,
                    cancellationToken);
        }

        public Task<bool> ExistsByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(normalizedCode);
            cancellationToken.ThrowIfCancellationRequested();

            return _context.Suppliers.AnyAsync(
                supplier => supplier.Code == normalizedCode,
                cancellationToken);
        }

        public async Task SaveAsync(Supplier supplier, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(supplier);
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
                var entry = _context.Entry(supplier);

                switch (entry.State)
                {
                    case EntityState.Detached:
                        _context.Suppliers.Add(supplier);
                        break;

                    case EntityState.Added:
                    case EntityState.Unchanged:
                    case EntityState.Modified:
                        break;

                    default:
                        throw new InvalidOperationException(
                            "Cannot save supplier in its current tracking state.");
                }

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "IX_Suppliers_Code",
                    TableName: "Suppliers"
                })
            {
                _saveFailed = true;
                throw new SupplierCodeConflictException(supplier.Code);
            }
            catch
            {
                _saveFailed = true;
                throw;
            }
        }
    }
}
