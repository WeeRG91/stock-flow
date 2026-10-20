using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Exceptions;

namespace StockFlow.Application.Suppliers.DeactivateSupplier
{
    public sealed class DeactivateSupplierCommandHandler
    {
        private readonly ISupplierRepository _suppliers;

        public DeactivateSupplierCommandHandler(ISupplierRepository suppliers)
        {
            ArgumentNullException.ThrowIfNull(suppliers);
            _suppliers = suppliers;
        }

        public async Task<SupplierDto> HandleAsync(
            DeactivateSupplierCommand command,
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

            supplier.Deactivate();

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
