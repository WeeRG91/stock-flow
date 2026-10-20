using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Domain.Entities;

namespace StockFlow.Application.Tests.Fakes
{
    public sealed class FakeSupplierRepository : ISupplierRepository
    {
        private readonly Dictionary<Guid, Supplier> _suppliers = new();

        public int GetByIdCallCount { get; private set; }
        public Guid LastRequestedSupplierId { get; private set; }
        public CancellationToken GetByIdToken { get; private set; }

        public Task<Supplier?> GetByIdAsync(
            Guid supplierId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            GetByIdCallCount++;
            LastRequestedSupplierId = supplierId;
            GetByIdToken = cancellationToken;

            if (LookupException is not null)
            {
                return Task.FromException<Supplier?>(LookupException);
            }

            _suppliers.TryGetValue(supplierId, out var supplier);
            return Task.FromResult(supplier);
        }

        public int ExistsCallCount { get; private set; }
        public int SaveCallCount { get; private set; }

        public string? LastCheckedCode { get; private set; }
        public Supplier? LastSavedSupplier { get; private set; }

        public CancellationToken LookupToken { get; private set; }
        public CancellationToken SaveToken { get; private set; }

        public Exception? LookupException { get; set; }
        public Exception? SaveException { get; set; }

        public void Add(Supplier supplier)
        {
            _suppliers.Add(supplier.Id, supplier);
        }

        public Task<bool> ExistsByCodeAsync(
            string normalizedCode,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ExistsCallCount++;
            LastCheckedCode = normalizedCode;
            LookupToken = cancellationToken;

            if (LookupException is not null)
            {
                return Task.FromException<bool>(LookupException);
            }

            return Task.FromResult(
                _suppliers.Values.Any(
                    supplier => supplier.Code == normalizedCode));
        }

        public Task SaveAsync(
            Supplier supplier,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SaveCallCount++;
            SaveToken = cancellationToken;

            if (SaveException is not null)
            {
                return Task.FromException(SaveException);
            }

            _suppliers[supplier.Id] = supplier;
            LastSavedSupplier = supplier;

            return Task.CompletedTask;
        }
    }
}
