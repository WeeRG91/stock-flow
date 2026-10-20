using StockFlow.Application.Exceptions;

namespace StockFlow.Application.Suppliers.GetSupplierById
{
    public sealed class GetSupplierByIdQueryHandler
    {
        private readonly ISupplierReadService _suppliers;

        public GetSupplierByIdQueryHandler(ISupplierReadService suppliers)
        {
            ArgumentNullException.ThrowIfNull(suppliers);
            _suppliers = suppliers;
        }

        public async Task<SupplierDto> HandleAsync(
            GetSupplierByIdQuery query,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();

            if (query.SupplierId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(query.SupplierId),
                    "Supplier ID must not be empty.");
            }

            var supplier = await _suppliers.GetSupplierByIdAsync(
                query.SupplierId,
                cancellationToken);

            if (supplier is null)
            {
                throw new ResourceNotFoundException(
                    "Supplier",
                    query.SupplierId);
            }

            return supplier;
        }
    }
}
