using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Abstractions.Time;
using StockFlow.Application.Exceptions;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Inventory.ReceiveStock
{
    public sealed class ReceiveStockCommandHandler
    {
        private readonly IProductRepository _products;
        private readonly IWarehouseRepository _warehouses;
        private readonly IInventoryRepository _inventories;
        private readonly IClock _clock;

        public ReceiveStockCommandHandler(
            IProductRepository products,
            IWarehouseRepository warehouses,
            IInventoryRepository inventories,
            IClock clock)
        {
            ArgumentNullException.ThrowIfNull(products, nameof(products));
            ArgumentNullException.ThrowIfNull(warehouses, nameof(warehouses));
            ArgumentNullException.ThrowIfNull(inventories, nameof(inventories));
            ArgumentNullException.ThrowIfNull(clock, nameof(clock));

            _products = products;
            _warehouses = warehouses;
            _inventories = inventories;
            _clock = clock;
        }

        public async Task<ReceiveStockResult> HandleAsync(
            ReceiveStockCommand command,
            CancellationToken cancellationToken = default)
        {
            Validate(command);
            cancellationToken.ThrowIfCancellationRequested();

            var product = await _products.GetByIdAsync(
                command.ProductId,
                cancellationToken);

            if (product is null)
            {
                throw new ResourceNotFoundException(
                   "Product",
                   command.ProductId);
            }

            if (!product.IsActive)
            {
                throw new BusinessRuleException("Product is inactive.");
            }

            var warehouse = await _warehouses.GetByIdAsync(
                command.WarehouseId,
                cancellationToken);

            if (warehouse is null)
            {
                throw new ResourceNotFoundException(
                    "Warehouse",
                    command.WarehouseId);
            }

            if (!warehouse.IsActive)
            {
                throw new BusinessRuleException("Warehouse is inactive.");
            }

            var inventory = await _inventories.GetByProductAndWarehouseAsync(
                command.ProductId,
                command.WarehouseId,
                cancellationToken);

            inventory ??= new InventoryEntity(
                command.ProductId,
                command.WarehouseId);

            try
            {
                inventory.ReceiveStock(
                    command.Quantity,
                    "Manual stock receipt",
                    _clock.UtcNow);
            }
            catch (OverflowException)
            {
                throw new BusinessRuleException(
                    "Receiving this quantity would exceed the supported stock limit.");
            }

            await _inventories.SaveAsync(inventory, cancellationToken);

            return new ReceiveStockResult(
                inventory.Id,
                inventory.QuantityOnHand);
        }

        private static void Validate(ReceiveStockCommand command)
        {
            ArgumentNullException.ThrowIfNull(command, nameof(command));

            if (command.ProductId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Product ID must not be empty.",
                    nameof(command.ProductId));
            }

            if (command.WarehouseId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Warehouse ID must not be empty.",
                    nameof(command.WarehouseId));
            }

            if (command.Quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(command.Quantity),
                    "Quantity must be greater than zero.");
            }
        }
    }
}
