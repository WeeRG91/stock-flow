using StockFlow.Application.Abstractions;
using StockFlow.Application.Abstractions.Notifications;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Abstractions.Time;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Transfers.DispatchTransfer
{
    public sealed class DispatchTransferCommandHandler
    {
        private readonly ITransferRepository _transfers;
        private readonly IInventoryRepository _inventories;
        private readonly IWarehouseRepository _warehouses;
        private readonly IProductRepository _products;
        private readonly ICurrentUser _currentUser;
        private readonly IClock _clock;
        private readonly ITransferChangeNotifier _transferNotifier;
        private readonly IInventoryChangeNotifier _inventoryNotifier;

        public DispatchTransferCommandHandler(
           ITransferRepository transfers,
           IInventoryRepository inventories,
           IWarehouseRepository warehouses,
           IProductRepository products,
           ICurrentUser currentUser,
           IClock clock,
           ITransferChangeNotifier transferNotifier,
           IInventoryChangeNotifier inventoryNotifier)
        {
            ArgumentNullException.ThrowIfNull(transfers);
            ArgumentNullException.ThrowIfNull(inventories);
            ArgumentNullException.ThrowIfNull(warehouses);
            ArgumentNullException.ThrowIfNull(products);
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(transferNotifier);
            ArgumentNullException.ThrowIfNull(inventoryNotifier);

            _transfers = transfers;
            _inventories = inventories;
            _warehouses = warehouses;
            _products = products;
            _currentUser = currentUser;
            _clock = clock;
            _transferNotifier = transferNotifier;
            _inventoryNotifier = inventoryNotifier;
        }

        public async Task HandleAsync(
            DispatchTransferCommand command,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            var actorId = await RequireDispatchingUserAsync(cancellationToken);

            var transfer = await LoadDispatchableTransferAsync(command, cancellationToken);

            await RequireActiveWarehouseAsync(transfer.SourceWarehouseId, cancellationToken);

            await RequireActiveWarehouseAsync(transfer.DestinationWarehouseId, cancellationToken);

            var prepared = await PrepareSourceStockAsync(transfer, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var dispatchedAt = _clock.UtcNow;

            transfer.Dispatch(dispatchedAt, actorId);

            foreach (var (line, inventory) in prepared)
            {
                inventory.RemoveStock(
                    line.Quantity,
                    $"Transfer {transfer.Id} dispatched",
                    dispatchedAt);
            }

            var sourceInventories = prepared
                .Select(item => item.Inventory)
                .ToArray();

            await _transfers.SaveDispatchAsync(
                transfer,
                sourceInventories,
                cancellationToken);

            await Task.WhenAll(
                _transferNotifier.NotifyAsync(
                    new TransferChanged(transfer.Id),
                    cancellationToken),
                _inventoryNotifier.NotifyAsync(
                    new InventoryChanged(),
                    cancellationToken));
        }

        private async Task<Guid> RequireDispatchingUserAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var actorId = _currentUser.UserId;

            if (actorId is null || actorId == Guid.Empty)
            {
                throw new AuthenticationRequiredException();
            }

            if (!await _currentUser.HasPermissionAsync(
                StockFlowPolicies.TransfersDispatch, cancellationToken))
            {
                throw new PermissionDeniedException();
            }

            return actorId.Value;
        }

        private async Task RequireActiveWarehouseAsync(
            Guid warehouseId,
            CancellationToken cancellationToken)
        {
            var warehouse = await _warehouses.GetByIdAsync(
                warehouseId, cancellationToken)
                ?? throw new ResourceNotFoundException(
                    "Warehouse", warehouseId);

            if (!warehouse.IsActive)
            {
                throw new BusinessRuleException(
                    $"Warehouse '{warehouse.Code}' is inactive.");
            }
        }

        private async Task<Transfer> LoadDispatchableTransferAsync(
            DispatchTransferCommand command,
            CancellationToken cancellationToken)
        {
            if (command.TransferId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.TransferId),
                    "Transfer ID must not be empty.");
            }

            if (command.ExpectedVersion == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.ExpectedVersion),
                    "Expected version must not be empty.");
            }

            var (transfer, version) = await _transfers.GetByIdAsync(
                command.TransferId, cancellationToken)
                ?? throw new ResourceNotFoundException(
                    "Transfer", command.TransferId);

            if (version != command.ExpectedVersion)
            {
                throw new TransferConflictException(
                    transfer.Id,
                    "The transfer has changed. Reload it and try again.");
            }

            if (transfer.Status != TransferStatus.Draft)
            {
                throw new BusinessRuleException(
                    "Only draft transfers can be dispatched.");
            }

            if (transfer.Lines.Count == 0)
            {
                throw new BusinessRuleException(
                    "A transfer must have at least one line before dispatch.");
            }

            return transfer;
        }

        private async Task<IReadOnlyList<(TransferLine Line, InventoryEntity Inventory)>> PrepareSourceStockAsync(
            Transfer transfer,
            CancellationToken cancellationToken)
        {
            var prepared = new List<(TransferLine Line, InventoryEntity Inventory)>();

            foreach (var line in transfer.Lines)
            {
                var product = await _products.GetByIdAsync(
                    line.ProductId, cancellationToken)
                    ?? throw new ResourceNotFoundException(
                        "Product", line.ProductId);

                if (!product.IsActive)
                {
                    throw new BusinessRuleException(
                        $"Product '{product.Sku}' is inactive.");
                }

                var inventory = await _inventories.GetByProductAndWarehouseAsync(
                    line.ProductId,
                    transfer.SourceWarehouseId,
                    cancellationToken);

                if (inventory is null ||
                    inventory.QuantityAvailable < line.Quantity)
                {
                    throw new BusinessRuleException(
                        $"Insufficient available source stock for product '{product.Sku}'.");
                }

                prepared.Add((line, inventory));
            }

            return prepared;
        }
    }
}
