using StockFlow.Application.Abstractions;
using StockFlow.Application.Abstractions.Notifications;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Abstractions.Time;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Transfers.ReceiveTransfer
{
    public sealed class ReceiveTransferCommandHandler
    {
        private readonly ICurrentUser _currentUser;
        private readonly ITransferReceiptRepository _receipts;
        private readonly ITransferRepository _transfers;
        private readonly IInventoryRepository _inventories;
        private readonly IClock _clock;
        private readonly ITransferChangeNotifier _transferNotifier;
        private readonly IInventoryChangeNotifier _inventoryNotifier;

        public ReceiveTransferCommandHandler(
            ICurrentUser currentUser,
            ITransferReceiptRepository receipts,
            ITransferRepository transfers,
            IInventoryRepository inventories,
            IClock clock,
            ITransferChangeNotifier transferNotifier,
            IInventoryChangeNotifier inventoryNotifier)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(receipts);
            ArgumentNullException.ThrowIfNull(transfers);
            ArgumentNullException.ThrowIfNull(inventories);
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(transferNotifier);
            ArgumentNullException.ThrowIfNull(inventoryNotifier);

            _currentUser = currentUser;
            _receipts = receipts;
            _transfers = transfers;
            _inventories = inventories;
            _clock = clock;
            _transferNotifier = transferNotifier;
            _inventoryNotifier = inventoryNotifier;
        }

        public async Task<ReceiveTransferResult> HandleAsync(
            ReceiveTransferCommand command,
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

            var transfer = await LoadReceivableTransferAsync(command, cancellationToken);

            ValidateReceiptLines(transfer, command);

            var prepared = await PrepareDestinationStockAsync(transfer, command, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var receipt = new TransferReceipt(
                command.ReceiptId,
                transfer.Id,
                _clock.UtcNow,
                userId,
                command.Lines
                    .Select(line => new TransferReceiptLine(
                        line.TransferLineId,
                        line.ReceivedQuantity))
                    .ToArray());

            transfer.ApplyReceipt(receipt);

            foreach (var (line, inventory) in prepared)
            {
                inventory.ReceiveStock(
                    line.ReceivedQuantity,
                    $"Transfer {transfer.Id} receipt {receipt.Id}",
                    receipt.ConfirmedAt);
            }

            var destinationInventories = prepared
                .Select(item => item.Inventory)
                .ToArray();

            await _receipts.SaveConfirmationAsync(
                receipt,
                transfer,
                destinationInventories,
                cancellationToken);

            await Task.WhenAll(
                _transferNotifier.NotifyAsync(
                    new TransferChanged(transfer.Id),
                    cancellationToken),
                _inventoryNotifier.NotifyAsync(
                    new InventoryChanged(),
                    cancellationToken));

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
                StockFlowPolicies.TransfersReceive, cancellationToken))
            {
                throw new PermissionDeniedException();
            }

            return userId.Value;
        }

        private async Task<ReceiveTransferResult?> FindConfirmedReceiptAsync(
            ReceiveTransferCommand command,
            CancellationToken cancellationToken)
        {
            var receipt = await _receipts.GetByIdAsync(
                command.ReceiptId, cancellationToken);

            if (receipt is null)
            {
                return null;
            }

            var requestedQuantities = command.Lines.ToDictionary(
                line => line.TransferLineId,
                line => line.ReceivedQuantity);

            if (receipt.TransferId != command.TransferId ||
                receipt.Lines.Count != requestedQuantities.Count ||
                receipt.Lines.Any(line =>
                    !requestedQuantities.TryGetValue(
                        line.TransferLineId, out var quantity) ||
                    quantity != line.ReceivedQuantity))
            {
                throw new TransferConflictException(
                    command.TransferId,
                    "This receipt ID has already been used for a different confirmation.");
            }

            return ToResult(receipt);
        }

        private async Task<Transfer> LoadReceivableTransferAsync(
            ReceiveTransferCommand command,
            CancellationToken cancellationToken)
        {
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

            if (transfer.Status is not
                (TransferStatus.Dispatched or TransferStatus.PartiallyReceived))
            {
                throw new BusinessRuleException(
                    "Only dispatched or partially received transfers can receive goods.");
            }

            return transfer;
        }

        private async Task<IReadOnlyList<(ReceiveTransferLineCommand Line, InventoryEntity Inventory)>> PrepareDestinationStockAsync(
            Transfer transfer,
            ReceiveTransferCommand command,
            CancellationToken cancellationToken)
        {
            var linesById = transfer.Lines.ToDictionary(line => line.Id);

            var prepared =
                new List<(ReceiveTransferLineCommand Line, InventoryEntity Inventory)>();

            foreach (var requested in command.Lines)
            {
                var transferLine = linesById[requested.TransferLineId];

                var inventory =
                    await _inventories.GetByProductAndWarehouseAsync(
                        transferLine.ProductId,
                        transfer.DestinationWarehouseId,
                        cancellationToken)
                    ?? new InventoryEntity(
                        transferLine.ProductId,
                        transfer.DestinationWarehouseId);

                if (requested.ReceivedQuantity >
                    int.MaxValue - inventory.QuantityOnHand)
                {
                    throw new BusinessRuleException(
                        $"Receiving would exceed the maximum stock quantity " +
                        $"for product '{transferLine.ProductSku}'.");
                }

                prepared.Add((requested, inventory));
            }

            return prepared;
        }

        private static void ValidateCommand(ReceiveTransferCommand command)
        {
            if (command.ReceiptId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.ReceiptId),
                    "Receipt ID must not be empty.");
            }

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

            if (command.Lines is null || command.Lines.Count == 0)
            {
                throw new RequestValidationException(
                    nameof(command.Lines),
                    "Provide at least one receipt line.");
            }

            var lineIds = new HashSet<Guid>();

            for (var index = 0; index < command.Lines.Count; index++)
            {
                var line = command.Lines[index];
                var field = $"Lines[{index}]";

                if (line is null)
                {
                    throw new RequestValidationException(
                        field, "A receipt line is required.");
                }

                if (line.TransferLineId == Guid.Empty)
                {
                    throw new RequestValidationException(
                        $"{field}.TransferLineId",
                        "Transfer line ID must not be empty.");
                }

                if (!lineIds.Add(line.TransferLineId))
                {
                    throw new RequestValidationException(
                        $"{field}.TransferLineId",
                        "A transfer line can appear only once per receipt.");
                }

                if (line.ReceivedQuantity <= 0)
                {
                    throw new RequestValidationException(
                        $"{field}.ReceivedQuantity",
                        "Received quantity must be greater than zero.");
                }
            }
        }

        private static void ValidateReceiptLines(
            Transfer transfer,
            ReceiveTransferCommand command)
        {
            var linesById = transfer.Lines.ToDictionary(line => line.Id);

            for (var index = 0; index < command.Lines.Count; index++)
            {
                var requested = command.Lines[index];

                if (!linesById.TryGetValue(
                    requested.TransferLineId, out var transferLine))
                {
                    throw new RequestValidationException(
                        $"Lines[{index}].TransferLineId",
                        "The receipt line must belong to this transfer.");
                }

                if (requested.ReceivedQuantity > transferLine.InTransitQuantity)
                {
                    throw new BusinessRuleException(
                        $"Received quantity exceeds the quantity in transit " +
                        $"for product '{transferLine.ProductSku}'.");
                }
            }
        }

        private static ReceiveTransferResult ToResult(TransferReceipt receipt)
        {
            return new ReceiveTransferResult(
                receipt.Id,
                receipt.TransferId,
                receipt.ConfirmedAt,
                receipt.ConfirmedByUserId,
                receipt.Lines
                    .Select(line => new ReceivedTransferLineDto(
                        line.TransferLineId,
                        line.ReceivedQuantity))
                    .ToArray());
        }
    }
}
