using StockFlow.Application.Exceptions;

namespace StockFlow.Application.Suppliers.GetSuppliers
{
    public sealed class GetSuppliersQueryHandler
    {
        private readonly ISupplierReadService _suppliers;

        public GetSuppliersQueryHandler(ISupplierReadService suppliers)
        {
            ArgumentNullException.ThrowIfNull(suppliers);
            _suppliers = suppliers;
        }

        public Task<GetSuppliersResult> HandleAsync(
            GetSuppliersQuery query,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();

            if (query.PageNumber < 1)
            {
                throw new RequestValidationException(
                    nameof(query.PageNumber),
                    "Page number must be at least 1.");
            }

            if (query.PageSize < 1 || query.PageSize > 100)
            {
                throw new RequestValidationException(
                    nameof(query.PageSize),
                    "Page size must be between 1 and 100.");
            }

            var offset = ((long)query.PageNumber - 1) * query.PageSize;

            if (offset > int.MaxValue)
            {
                throw new RequestValidationException(
                    nameof(query.PageNumber),
                    "The requested page is beyond the supported range.");
            }

            var search = string.IsNullOrWhiteSpace(query.Search)
                ? null
                : query.Search.Trim();

            return _suppliers.GetSuppliersAsync(
                query.PageNumber,
                query.PageSize,
                search,
                query.IsActive,
                cancellationToken);
        }
    }
}
