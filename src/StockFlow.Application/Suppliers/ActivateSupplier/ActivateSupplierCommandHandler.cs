using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Exceptions;

namespace StockFlow.Application.Suppliers.ActivateSupplier
{
    public sealed class ActivateSupplierCommandHandler
    {
        private readonly ISupplierRepository _suppliers;

        public ActivateSupplierCommandHandler(ISupplierRepository suppliers)
        {
            ArgumentNullException.ThrowIfNull(suppliers);
            _suppliers = suppliers;
        }

        public async Task<SupplierDto> HandleAsync(
            ActivateSupplierCommand command,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            cancellationToken.ThrowIfCancellationRequested();

            if (command.SupplierId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.SupplierId),
                    "Supplier ID must not be empty.");
            }

            var supplier = await _suppliers.GetByIdAsync(
                command.SupplierId,
                cancellationToken);

            if (supplier is null)
            {
                throw new ResourceNotFoundException(
                    "Supplier",
                    command.SupplierId);
            }

            supplier.Activate();

            await _suppliers.SaveAsync(supplier, cancellationToken);

            return new SupplierDto(
                supplier.Id,
                supplier.Code,
                supplier.Name,
                supplier.ContactEmail,
                supplier.PhoneNumber,
                supplier.IsActive);
        }
    }
}
