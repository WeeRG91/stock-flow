using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Exceptions;

namespace StockFlow.Application.Suppliers.UpdateSupplier
{
    public sealed class UpdateSupplierCommandHandler
    {
        private readonly ISupplierRepository _suppliers;

        public UpdateSupplierCommandHandler(ISupplierRepository supplier)
        {
            ArgumentNullException.ThrowIfNull(supplier);
            _suppliers = supplier;
        }

        public async Task<SupplierDto> HandleAsync(
            UpdateSupplierCommand command,
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

            try
            {
                supplier.UpdateDetails(
                    command.Name,
                    command.ContactEmail,
                    command.PhoneNumber);
            }
            catch (ArgumentException exception)
                when (exception.ParamName is
                    "name" or "contactEmail" or "phoneNumber")
            {
                var fieldName = exception.ParamName switch
                {
                    "name" => nameof(UpdateSupplierCommand.Name),
                    "contactEmail" =>
                        nameof(UpdateSupplierCommand.ContactEmail),
                    _ => nameof(UpdateSupplierCommand.PhoneNumber)
                };

                throw new RequestValidationException(
                    fieldName,
                    exception.Message);
            }

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
