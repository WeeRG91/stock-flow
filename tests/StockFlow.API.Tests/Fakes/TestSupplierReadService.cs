using StockFlow.Application.Suppliers;
using StockFlow.Application.Suppliers.GetSuppliers;

namespace StockFlow.API.Tests.Fakes
{
    public sealed class TestSupplierReadService : ISupplierReadService
    {
        public GetSuppliersResult Result { get; set; } =
            new(Array.Empty<SupplierDto>(), 1, 20, 0);

        public Exception? ReadException { get; set; }

        public int CallCount { get; private set; }
        public int PageNumber { get; private set; }
        public int PageSize { get; private set; }
        public string? Search { get; private set; }
        public bool? IsActive { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }
        public SupplierDto? SupplierByIdResult { get; set; }
        public int ByIdCallCount { get; private set; }
        public Guid ReceivedSupplierId { get; private set; }

        public Task<GetSuppliersResult> GetSuppliersAsync(
            int pageNumber,
            int pageSize,
            string? search,
            bool? isActive,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;
            PageNumber = pageNumber;
            PageSize = pageSize;
            Search = search;
            IsActive = isActive;
            ReceivedToken = cancellationToken;

            if (ReadException is not null)
            {
                return Task.FromException<GetSuppliersResult>(
                    ReadException);
            }

            return Task.FromResult(Result);
        }

        public Task<SupplierDto?> GetSupplierByIdAsync(
            Guid supplierId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ByIdCallCount++;
            ReceivedSupplierId = supplierId;
            ReceivedToken = cancellationToken;

            if (ReadException is not null)
            {
                return Task.FromException<SupplierDto?>(ReadException);
            }

            return Task.FromResult(SupplierByIdResult);
        }
    }
}