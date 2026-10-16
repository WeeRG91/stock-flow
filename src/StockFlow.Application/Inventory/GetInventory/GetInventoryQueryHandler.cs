using StockFlow.Application.Exceptions;

namespace StockFlow.Application.Inventory.GetInventory
{
    public sealed class GetInventoryQueryHandler
    {
        private readonly IInventoryReadService _inventory;

        public GetInventoryQueryHandler(IInventoryReadService inventory)
        {
            ArgumentNullException.ThrowIfNull(inventory);
            _inventory = inventory;
        }

        public Task<GetInventoryResult> HandleAsync(
            GetInventoryQuery query,
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

            return _inventory.GetInventoryAsync(
                query.PageNumber,
                query.PageSize,
                cancellationToken);
        }
    }
}
