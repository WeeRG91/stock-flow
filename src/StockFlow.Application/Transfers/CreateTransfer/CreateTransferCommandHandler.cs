using StockFlow.Application.Abstractions;
using StockFlow.Application.Abstractions.Notifications;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Abstractions.Time;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;

namespace StockFlow.Application.Transfers.CreateTransfer
{
    public sealed class CreateTransferCommandHandler
    {
        private readonly ICurrentUser _currentUser;
        private readonly IWarehouseRepository _warehouses;
        private readonly IProductRepository _products;
        private readonly ITransferRepository _transfers;
        private readonly IClock _clock;

        private readonly ITransferChangeNotifier _notifier;

        public CreateTransferCommandHandler(
            ICurrentUser currentUser,
            IWarehouseRepository warehouses,
            IProductRepository products,
            ITransferRepository transfers,
            IClock clock,
            ITransferChangeNotifier notifier)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(warehouses);
            ArgumentNullException.ThrowIfNull(products);
            ArgumentNullException.ThrowIfNull(transfers);
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(notifier);

            _currentUser = currentUser;
            _warehouses = warehouses;
            _products = products;
            _transfers = transfers;
            _clock = clock;
            _notifier = notifier;
        }

        public async Task<CreateTransferResult> HandleAsync(
            CreateTransferCommand command,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            var userId = await RequireManagingUserAsync(cancellationToken);
            ValidateCommand(command);

            var source = await LoadActiveWarehouseAsync(
                command.SourceWarehouseId, cancellationToken);

            var destination = await LoadActiveWarehouseAsync(
                command.DestinationWarehouseId, cancellationToken);

            var products = await LoadActiveProductsAsync(
                command.Lines, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var transfer = new Transfer(
                source.Id,
                source.Code,
                source.Name,
                destination.Id,
                destination.Code,
                destination.Name,
                _clock.UtcNow,
                userId);

            foreach (var line in command.Lines)
            {
                var product = products[line.ProductId];

                transfer.AddLine(
                    product.Id, product.Sku, product.Name, line.Quantity);
            }

            await _transfers.SaveDraftAsync(transfer, cancellationToken);

            await _notifier.NotifyAsync(
                new TransferChanged(transfer.Id),
                cancellationToken);

            return new CreateTransferResult(
                transfer.Id,
                transfer.Status,
                transfer.CreatedAt,
                transfer.CreatedByUserId);
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
                StockFlowPolicies.TransfersManage, cancellationToken))
            {
                throw new PermissionDeniedException();
            }

            return userId.Value;
        }

        private async Task<Warehouse> LoadActiveWarehouseAsync(
            Guid warehouseId,
            CancellationToken cancellationToken)
        {
            var warehouse = await _warehouses.GetByIdAsync(
                warehouseId, cancellationToken)
                ?? throw new ResourceNotFoundException("Warehouse", warehouseId);

            if (!warehouse.IsActive)
            {
                throw new BusinessRuleException("The selected warehouse is inactive.");
            }

            return warehouse;
        }

        private async Task<IReadOnlyDictionary<Guid, Product>> LoadActiveProductsAsync(
            IReadOnlyList<CreateTransferLineCommand> lines,
            CancellationToken cancellationToken)
        {
            var products = new Dictionary<Guid, Product>();

            foreach (var line in lines)
            {
                var product = await _products.GetByIdAsync(
                    line.ProductId, cancellationToken)
                    ?? throw new ResourceNotFoundException("Product", line.ProductId);

                if (!product.IsActive)
                {
                    throw new BusinessRuleException(
                        $"Product '{product.Sku}' is inactive.");
                }

                products.Add(product.Id, product);
            }

            return products;
        }

        private static void ValidateCommand(CreateTransferCommand command)
        {
            if (command.SourceWarehouseId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.SourceWarehouseId),
                    "Source warehouse ID must not be empty.");
            }

            if (command.DestinationWarehouseId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.DestinationWarehouseId),
                    "Destination warehouse ID must not be empty.");
            }

            if (command.SourceWarehouseId == command.DestinationWarehouseId)
            {
                throw new RequestValidationException(
                    nameof(command.DestinationWarehouseId),
                    "Source and destination warehouses must differ.");
            }

            if (command.Lines is null)
            {
                throw new RequestValidationException(
                    nameof(command.Lines),
                    "Provide a lines collection. It may be empty for a draft.");
            }

            var productIds = new HashSet<Guid>();

            for (var index = 0; index < command.Lines.Count; index++)
            {
                var line = command.Lines[index];
                var field = $"{nameof(command.Lines)}[{index}]";

                if (line is null)
                {
                    throw new RequestValidationException(
                        field, "A transfer line is required.");
                }

                if (line.ProductId == Guid.Empty)
                {
                    throw new RequestValidationException(
                        $"{field}.{nameof(line.ProductId)}",
                        "Product ID must not be empty.");
                }

                if (!productIds.Add(line.ProductId))
                {
                    throw new RequestValidationException(
                        $"{field}.{nameof(line.ProductId)}",
                        "A product can appear only once per transfer.");
                }

                if (line.Quantity <= 0)
                {
                    throw new RequestValidationException(
                        $"{field}.{nameof(line.Quantity)}",
                        "Quantity must be greater than zero.");
                }
            }
        }
    }
}
