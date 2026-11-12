using StockFlow.Application.Abstractions;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.Application.PurchaseOrders.UpdateDraftPurchaseOrder
{
    public sealed class UpdateDraftPurchaseOrderCommandHandler
    {
        private readonly ICurrentUser _currentUser;
        private readonly IPurchaseOrderRepository _orders;
        private readonly IProductRepository _products;

        public UpdateDraftPurchaseOrderCommandHandler(
            ICurrentUser currentUser,
            IPurchaseOrderRepository orders,
            IProductRepository products)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(orders);
            ArgumentNullException.ThrowIfNull(products);

            _currentUser = currentUser;
            _orders = orders;
            _products = products;
        }

        public async Task HandleAsync(
            UpdateDraftPurchaseOrderCommand command,
            CancellationToken cancellationToken = default)
        {
            var order = await LoadDraftAsync(command, cancellationToken);
            var lines = command.Lines.ToArray();
            var existingByProduct = order.Lines.ToDictionary(line => line.ProductId);
            var seenProducts = new HashSet<Guid>();
            var drafts = new List<PurchaseOrderLineDraft>(lines.Length);

            for (var index = 0; index < lines.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var line = lines[index];
                var field = $"{nameof(command.Lines)}[{index}]";

                if (line is null)
                {
                    throw new RequestValidationException(field, "An order line is required.");
                }

                var productField = $"{field}.{nameof(line.ProductId)}";

                if (line.ProductId == Guid.Empty)
                {
                    throw new RequestValidationException(
                        productField, "Product ID must not be empty.");
                }

                if (!seenProducts.Add(line.ProductId))
                {
                    throw new RequestValidationException(
                        productField, "A product may appear only once per order.");
                }

                if (existingByProduct.TryGetValue(line.ProductId, out var existing))
                {
                    drafts.Add(new PurchaseOrderLineDraft(
                        existing.ProductId, existing.ProductSku, existing.ProductName,
                        line.OrderedQuantity, line.UnitPrice));

                    continue;
                }

                var product = await _products.GetByIdAsync(
                    line.ProductId, cancellationToken)
                    ?? throw new ResourceNotFoundException("Product", line.ProductId);

                if (!product.IsActive)
                {
                    throw new BusinessRuleException("Product is inactive.");
                }

                drafts.Add(new PurchaseOrderLineDraft(
                    product.Id, product.Sku, product.Name,
                    line.OrderedQuantity, line.UnitPrice));
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                order.ReplaceDraftLines(drafts);
            }
            catch (ArgumentException exception)
                when (exception.ParamName is "orderedQuantity" or "unitPrice")
            {
                throw new RequestValidationException(nameof(command.Lines), exception.Message);
            }
            catch (OverflowException)
            {
                throw new RequestValidationException(
                    nameof(command.Lines), "The line or order total exceeds the supported range.");
            }

            await _orders.SaveAsync(order, cancellationToken);
        }

        private async Task<PurchaseOrder> LoadDraftAsync(
            UpdateDraftPurchaseOrderCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);
            cancellationToken.ThrowIfCancellationRequested();

            var userId = _currentUser.UserId;
            if (userId is null || userId == Guid.Empty)
            {
                throw new AuthenticationRequiredException();
            }

            if (!await _currentUser.HasPermissionAsync(
                StockFlowPolicies.PurchaseOrdersManage, cancellationToken))
            {
                throw new PermissionDeniedException();
            }

            if (command.PurchaseOrderId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.PurchaseOrderId),
                    "Purchase order ID must not be empty.");
            }

            if (command.ExpectedVersion == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.ExpectedVersion),
                    "Expected version must not be empty.");
            }

            if (command.Lines is null)
            {
                throw new RequestValidationException(
                    nameof(command.Lines),
                    "Provide a lines collection. It may be empty for a draft.");
            }

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

            if (order.Status != PurchaseOrderStatus.Draft)
            {
                throw new BusinessRuleException(
                    "Only draft purchase orders can be edited.");
            }

            return order;
        }
    }
}
