using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;

namespace StockFlow.Application.Suppliers.CreateSupplier
{
    public sealed class CreateSupplierCommandHandler
    {
        private readonly ISupplierRepository _suppliers;

        public CreateSupplierCommandHandler(ISupplierRepository suppliers)
        {
            ArgumentNullException.ThrowIfNull(suppliers);
            _suppliers = suppliers;
        }

        public async Task<CreateSupplierResult> HandleAsync(
            CreateSupplierCommand command,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            cancellationToken.ThrowIfCancellationRequested();

            Supplier supplier;

            try
            {
                supplier = new Supplier(
                    command.Code,
                    command.Name,
                    command.ContactEmail,
                    command.PhoneNumber);
            }
            catch (ArgumentException exception)
                when (exception.ParamName is
                    "code" or "name" or "contactEmail" or "phoneNumber")
            {
                var fieldName = exception.ParamName switch
                {
                    "code" => nameof(CreateSupplierCommand.Code),
                    "name" => nameof(CreateSupplierCommand.Name),
                    "contactEmail" => nameof(CreateSupplierCommand.ContactEmail),
                    _ => nameof(CreateSupplierCommand.PhoneNumber)
                };

                throw new RequestValidationException(
                    fieldName,
                    exception.Message);
            }

            var codeExists = await _suppliers.ExistsByCodeAsync(
                supplier.Code,
                cancellationToken);

            if (codeExists)
            {
                throw new SupplierCodeConflictException(supplier.Code);
            }

            await _suppliers.SaveAsync(supplier, cancellationToken);

            return new CreateSupplierResult(
                supplier.Id,
                supplier.Code,
                supplier.Name,
                supplier.ContactEmail,
                supplier.PhoneNumber,
                supplier.IsActive);
        }
    }
}
