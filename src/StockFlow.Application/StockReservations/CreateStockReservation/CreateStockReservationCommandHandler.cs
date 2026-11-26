using StockFlow.Application.Abstractions;
using StockFlow.Application.Abstractions.Notifications;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Abstractions.Time;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.StockReservations.CreateStockReservation
{
    public sealed class CreateStockReservationCommandHandler
    {
        private readonly ICurrentUser _currentUser;
        private readonly IInventoryRepository _inventories;
        private readonly IProductRepository _products;
        private readonly IWarehouseRepository _warehouses;
        private readonly IStockReservationRepository _reservations;
        private readonly IClock _clock;
        private readonly IInventoryChangeNotifier _notifier;

        public CreateStockReservationCommandHandler(
            ICurrentUser currentUser,
            IInventoryRepository inventories,
            IProductRepository products,
            IWarehouseRepository warehouses,
            IStockReservationRepository reservations,
            IClock clock,
            IInventoryChangeNotifier notifier)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(inventories);
            ArgumentNullException.ThrowIfNull(products);
            ArgumentNullException.ThrowIfNull(warehouses);
            ArgumentNullException.ThrowIfNull(reservations);
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(notifier);

            _currentUser = currentUser;
            _inventories = inventories;
            _products = products;
            _warehouses = warehouses;
            _reservations = reservations;
            _clock = clock;
            _notifier = notifier;
        }

        public async Task<CreateStockReservationResult> HandleAsync(
            CreateStockReservationCommand command,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            var userId = await RequireManagingUserAsync(cancellationToken);
            ValidateCommand(command);

            var existing = await FindExistingCreationAsync(
                command,
                userId,
                cancellationToken);

            if (existing is not null)
            {
                return existing;
            }

            var inventory = await LoadEligibleInventoryAsync(
                command.InventoryId,
                cancellationToken);

            if (command.Quantity > inventory.QuantityAvailable)
            {
                throw new BusinessRuleException(
                    "Insufficient available stock for this reservation.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var reservation = new StockReservation(
                inventory.Id,
                command.Quantity,
                command.Reference,
                _clock.UtcNow,
                userId);

            inventory.ReserveStock(command.Quantity);

            await _reservations.SaveCreationAsync(
                reservation,
                inventory,
                command.RequestId,
                cancellationToken);

            await _notifier.NotifyAsync(
                new InventoryChanged(),
                cancellationToken);

            return new CreateStockReservationResult(
                reservation.Id,
                reservation.InventoryId,
                reservation.Quantity,
                reservation.Reference,
                reservation.Status,
                reservation.CreatedAt,
                reservation.CreatedByUserId);
        }

        private async Task<Guid> RequireManagingUserAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var userId = _currentUser.UserId;

            if (userId is null || userId == Guid.Empty)
            {
                throw new AuthenticationRequiredException();
            }

            if (!await _currentUser.HasPermissionAsync(
                StockFlowPolicies.StockReservationsManage,
                cancellationToken))
            {
                throw new PermissionDeniedException();
            }

            return userId.Value;
        }

        private async Task<CreateStockReservationResult?> FindExistingCreationAsync(
            CreateStockReservationCommand command,
            Guid userId,
            CancellationToken cancellationToken)
        {
            var existing = await _reservations.GetByRequestIdAsync(
                command.RequestId,
                cancellationToken);

            if (existing is null)
            {
                return null;
            }

            if (existing.CreatedByUserId != userId ||
                existing.InventoryId != command.InventoryId ||
                existing.Quantity != command.Quantity ||
                !string.Equals(
                    existing.Reference,
                    command.Reference.Trim(),
                    StringComparison.Ordinal))
            {
                throw new StockReservationConflictException(
                    existing.Id,
                    "This request ID has already been used for a different reservation request.");
            }

            return new CreateStockReservationResult(
                existing.Id,
                existing.InventoryId,
                existing.Quantity,
                existing.Reference,
                existing.Status,
                existing.CreatedAt,
                existing.CreatedByUserId);
        }

        private async Task<InventoryEntity> LoadEligibleInventoryAsync(
            Guid inventoryId,
            CancellationToken cancellationToken)
        {
            var inventory = await _inventories.GetByIdAsync(
                inventoryId,
                cancellationToken)
                ?? throw new ResourceNotFoundException(
                    "Inventory", inventoryId);

            var product = await _products.GetByIdAsync(
                inventory.ProductId,
                cancellationToken)
                ?? throw new ResourceNotFoundException(
                    "Product", inventory.ProductId);

            if (!product.IsActive)
            {
                throw new BusinessRuleException(
                    "Cannot reserve stock for an inactive product.");
            }

            var warehouse = await _warehouses.GetByIdAsync(
                inventory.WarehouseId,
                cancellationToken)
                ?? throw new ResourceNotFoundException(
                    "Warehouse", inventory.WarehouseId);

            if (!warehouse.IsActive)
            {
                throw new BusinessRuleException(
                    "Cannot reserve stock in an inactive warehouse.");
            }

            return inventory;
        }

        private static void ValidateCommand(
            CreateStockReservationCommand command)
        {
            if (command.RequestId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.RequestId),
                    "Request ID must not be empty.");
            }

            if (command.InventoryId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.InventoryId),
                    "Inventory ID must not be empty.");
            }

            if (command.Quantity <= 0)
            {
                throw new RequestValidationException(
                    nameof(command.Quantity),
                    "Quantity must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(command.Reference))
            {
                throw new RequestValidationException(
                    nameof(command.Reference),
                    "Reference must not be empty or whitespace.");
            }
        }
    }
}
