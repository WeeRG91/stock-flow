using StockFlow.Application.Abstractions;
using StockFlow.Application.Abstractions.Notifications;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Abstractions.Time;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.GoodsReceipts.ConfirmGoodsReceipt
{
    public sealed class ConfirmGoodsReceiptCommandHandler
    {
        private readonly ICurrentUser _currentUser;
        private readonly IGoodsReceiptRepository _receipts;
        private readonly IPurchaseOrderRepository _orders;
        private readonly IInventoryRepository _inventories;
        private readonly IClock _clock;
        private readonly IPurchasingChangeNotifier _purchasingNotifier;
        private readonly IInventoryChangeNotifier _inventoryNotifier;

        public ConfirmGoodsReceiptCommandHandler(
            ICurrentUser currentUser,
            IGoodsReceiptRepository receipts,
            IPurchaseOrderRepository orders,
            IInventoryRepository inventories,
            IClock clock,
            IPurchasingChangeNotifier purchasingNotifer,
            IInventoryChangeNotifier inventoryNotifier)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(receipts);
            ArgumentNullException.ThrowIfNull(orders);
            ArgumentNullException.ThrowIfNull(inventories);
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(purchasingNotifer);
            ArgumentNullException.ThrowIfNull(inventoryNotifier);

            _currentUser = currentUser;
            _receipts = receipts;
            _orders = orders;
            _inventories = inventories;
            _clock = clock;
            _purchasingNotifier = purchasingNotifer;
            _inventoryNotifier = inventoryNotifier;
        }

        public async Task<ConfirmGoodsReceiptResult> HandleAsync(
            ConfirmGoodsReceiptCommand command,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            var userId = await RequireReceivingUserAsync(cancellationToken);
            ValidateCommand(command);

            var existingResult = await FindConfirmedReceiptAsync(command, cancellationToken);
            if (existingResult is not null)
            {
                return existingResult;
            }

            var order = await LoadOrderForConfirmationAsync(command, cancellationToken);
            var receipt = ApplyReceiptToOrder(command, order, userId);
            var inventories = await ReceiveIntoInventoryAsync(order, receipt, cancellationToken);

            await _receipts.SaveConfirmationAsync(
                receipt, order, inventories, cancellationToken);

            await Task.WhenAll(
                _purchasingNotifier.NotifyAsync(Array.Empty<Guid>(), cancellationToken),
                _inventoryNotifier.NotifyAsync(new InventoryChanged(), cancellationToken));

            return ToResult(receipt);
        }

        private async Task<Guid> RequireReceivingUserAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var userId = _currentUser.UserId;
            if (userId is null || userId == Guid.Empty)
            {
                throw new AuthenticationRequiredException();
            }

            if (!await _currentUser.HasPermissionAsync(
                StockFlowPolicies.InventoryReceive, cancellationToken))
            {
                throw new PermissionDeniedException();
            }

            return userId.Value;
        }

        private async Task<ConfirmGoodsReceiptResult?> FindConfirmedReceiptAsync(
            ConfirmGoodsReceiptCommand command,
            CancellationToken cancellationToken)
        {
            var receipt = await _receipts.GetByIdAsync(
                command.ReceiptId, cancellationToken);

            if (receipt is null)
            {
                return null;
            }

            var requestedQuantities = command.Lines.ToDictionary(
                line => line.PurchaseOrderLineId,
                line => line.ReceivedQuantity);

            if (receipt.PurchaseOrderId != command.PurchaseOrderId
                || receipt.Lines.Count != requestedQuantities.Count
                || receipt.Lines.Any(line => 
                    !requestedQuantities.TryGetValue(line.PurchaseOrderLineId, out var quantity)
                    || quantity != line.ReceivedQuantity))
            {
                throw new PurchaseOrderConflictException(
                    command.PurchaseOrderId,
                    "This receipt ID has already been used for a different confirmation.");
            }

            return ToResult(receipt);
        }

        private async Task<PurchaseOrder> LoadOrderForConfirmationAsync(
            ConfirmGoodsReceiptCommand command,
            CancellationToken cancellationToken)
        {
            var (order, version) = await _orders.GetByIdAsync(
                command.PurchaseOrderId, cancellationToken)
                ?? throw new ResourceNotFoundException(
                    "Purchase order", command.PurchaseOrderId);

            if (version != command.ExpectedVersion)
            {
                throw new PurchaseOrderConflictException(
                    order.Id,
                    "The purchase order has changed. Reload it and try again.");
            }

            return order;
        }

        private GoodsReceipt ApplyReceiptToOrder(
            ConfirmGoodsReceiptCommand command,
            PurchaseOrder order,
            Guid userId)
        {
            var lines = command.Lines
                .Select(line => new GoodsReceiptLine(
                    line.PurchaseOrderLineId,
                    line.ReceivedQuantity))
                .ToArray();

            var receipt = new GoodsReceipt(
                command.ReceiptId,
                order.Id,
                _clock.UtcNow,
                userId,
                lines);

            var orderLineIds = order.Lines.Select(line => line.Id).ToHashSet();
            if (receipt.Lines.Any(line => !orderLineIds.Contains(line.PurchaseOrderLineId)))
            {
                throw new RequestValidationException(
                    nameof(command.Lines),
                    "Every receipt line must reference a line on this purchase order.");
            }

            try
            {
                order.ApplyReceipt(receipt);
            }
            catch (InvalidOperationException exception)
            {
                throw new BusinessRuleException(exception.Message);
            }

            return receipt;
        }

        private async Task<IReadOnlyList<InventoryEntity>> ReceiveIntoInventoryAsync(
            PurchaseOrder order,
            GoodsReceipt receipt,
            CancellationToken cancellationToken)
        {
            var orderLinesById = order.Lines.ToDictionary(line => line.Id);
            var delivery = new List<(InventoryEntity Balance, int Quantity)>();

            foreach (var line in receipt.Lines)
            {
                var productId = orderLinesById[line.PurchaseOrderLineId].ProductId;
                var balance = await _inventories.GetByProductAndWarehouseAsync(
                    productId, order.WarehouseId, cancellationToken)
                    ?? new InventoryEntity(productId, order.WarehouseId);

                if (line.ReceivedQuantity > int.MaxValue - balance.QuantityOnHand)
                {
                    throw new BusinessRuleException(
                        "Receiving this delivery would exceed the inventory quantity limit.");
                }

                delivery.Add((balance, line.ReceivedQuantity));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var reason = $"Goods receipt {receipt.Id}";

            foreach (var (balance, quantity) in delivery)
            {
                balance.ReceiveStock(quantity, reason, receipt.ConfirmedAt);
            }

            return delivery.Select(item => item.Balance).ToArray();
        }

        private static ConfirmGoodsReceiptResult ToResult(GoodsReceipt receipt)
        {
            return new ConfirmGoodsReceiptResult(
                receipt.Id,
                receipt.PurchaseOrderId,
                receipt.ConfirmedAt,
                receipt.ConfirmedByUserId,
                receipt.Lines.Select(line => new ConfirmedGoodsReceiptLineDto(
                    line.PurchaseOrderLineId,
                    line.ReceivedQuantity)).ToArray());
        }

        private static void ValidateCommand(ConfirmGoodsReceiptCommand command)
        {
            if (command.ReceiptId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.ReceiptId), "Receipt ID must not be empty.");
            }

            if (command.PurchaseOrderId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.PurchaseOrderId), "Purchase order ID must not be empty.");
            }

            if (command.ExpectedVersion == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.ExpectedVersion), "Expected version must not be empty.");
            }

            if (command.Lines is null || command.Lines.Count == 0)
            {
                throw new RequestValidationException(
                    nameof(command.Lines), "Provide at least one receipt line.");
            }

            var seenOrderLineIds = new HashSet<Guid>();

            for (var index = 0; index < command.Lines.Count; index++)
            {
                var line = command.Lines[index];
                var field = $"{nameof(command.Lines)}[{index}]";

                if (line is null)
                {
                    throw new RequestValidationException(field, "A receipt line is required.");
                }

                var orderLineIdField = $"{field}.{nameof(line.PurchaseOrderLineId)}";

                if (line.PurchaseOrderLineId == Guid.Empty)
                {
                    throw new RequestValidationException(
                        orderLineIdField, "Purchase order line ID must not be empty.");
                }

                if (!seenOrderLineIds.Add(line.PurchaseOrderLineId))
                {
                    throw new RequestValidationException(
                        orderLineIdField, "A purchase order line can appear only once per receipt.");
                }

                if (line.ReceivedQuantity <= 0)
                {
                    throw new RequestValidationException(
                        $"{field}.{nameof(line.ReceivedQuantity)}",
                        "Received quantity must be greater than zero.");
                }
            }
        }
    }
}
