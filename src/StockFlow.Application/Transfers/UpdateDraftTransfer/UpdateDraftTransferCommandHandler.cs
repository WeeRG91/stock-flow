using StockFlow.Application.Abstractions;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.Application.Transfers.UpdateDraftTransfer
{
    public sealed class UpdateDraftTransferCommandHandler
    {
        private readonly ITransferRepository _transfers;
        private readonly ICurrentUser _currentUser;
        private readonly IProductRepository _products;

        public UpdateDraftTransferCommandHandler(
            ITransferRepository transfers,
            ICurrentUser currentUser,
            IProductRepository products)
        {
            ArgumentNullException.ThrowIfNull(transfers);
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(products);

            _transfers = transfers;
            _currentUser = currentUser;
            _products = products;
        }

        public async Task HandleAsync(
            UpdateDraftTransferCommand command,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            await RequireManagingUserAsync(cancellationToken);
            ValidateLines(command.Lines);

            var transfer = await LoadDraftAsync(command, cancellationToken);
            var drafts = await PrepareLinesAsync(transfer, command.Lines, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            transfer.ReplaceDraftLines(drafts);
            await _transfers.SaveDraftAsync(transfer, cancellationToken);
        }

        private async Task RequireManagingUserAsync(
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
        }

        private async Task<Transfer> LoadDraftAsync(
            UpdateDraftTransferCommand command,
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
                    "Only draft transfers can be edited.");
            }

            return transfer;
        }

        private async Task<IReadOnlyList<TransferLineDraft>> PrepareLinesAsync(
            Transfer transfer,
            IReadOnlyList<UpdateDraftTransferLineCommand> lines,
            CancellationToken cancellationToken)
        {
            var existingByProduct = transfer.Lines.ToDictionary(line => line.ProductId);
            var drafts = new List<TransferLineDraft>();

            foreach (var line in lines)
            {
                if (existingByProduct.TryGetValue(line.ProductId, out var existing))
                {
                    drafts.Add(new TransferLineDraft(
                        existing.ProductId,
                        existing.ProductSku,
                        existing.ProductName,
                        line.Quantity));

                    continue;
                }

                var product = await _products.GetByIdAsync(
                    line.ProductId, cancellationToken)
                    ?? throw new ResourceNotFoundException("Product", line.ProductId);

                if (!product.IsActive)
                {
                    throw new BusinessRuleException(
                        $"Product '{product.Sku}' is inactive.");
                }

                drafts.Add(new TransferLineDraft(
                    product.Id,
                    product.Sku,
                    product.Name,
                    line.Quantity));
            }

            return drafts;
        }

        private static void ValidateLines(
            IReadOnlyList<UpdateDraftTransferLineCommand> lines)
        {
            if (lines is null)
            {
                throw new RequestValidationException(
                    "Lines", "Provide a lines collection. It may be empty for a draft.");
            }

            var productIds = new HashSet<Guid>();

            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var field = $"Lines[{index}]";

                if (line is null)
                {
                    throw new RequestValidationException(
                        field, "A transfer line is required.");
                }

                if (line.ProductId == Guid.Empty)
                {
                    throw new RequestValidationException(
                        $"{field}.ProductId", "Product ID must not be empty.");
                }

                if (!productIds.Add(line.ProductId))
                {
                    throw new RequestValidationException(
                        $"{field}.ProductId",
                        "A product can appear only once per transfer.");
                }

                if (line.Quantity <= 0)
                {
                    throw new RequestValidationException(
                        $"{field}.Quantity", "Quantity must be greater than zero.");
                }
            }
        }
    }
}
