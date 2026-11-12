using StockFlow.Application.Abstractions;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Abstractions.Purchasing;
using StockFlow.Application.Abstractions.Time;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Entities;

namespace StockFlow.Application.PurchaseOrders.CreatePurchaseOrder
{
    public sealed class CreatePurchaseOrderCommandHandler
    {
        private readonly ICurrentUser _currentUser;
        private readonly ISupplierRepository _suppliers;
        private readonly IWarehouseRepository _warehouses;
        private readonly IProductRepository _products;
        private readonly IPurchaseOrderRepository _purchaseOrders;
        private readonly IPurchaseOrderNumberGenerator _orderNumbers;
        private readonly IClock _clock;

        public CreatePurchaseOrderCommandHandler(
            ICurrentUser currentUser,
            ISupplierRepository suppliers,
            IWarehouseRepository warehouses,
            IProductRepository products,
            IPurchaseOrderRepository purchaseOrders,
            IPurchaseOrderNumberGenerator orderNumbers,
            IClock clock)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(suppliers);
            ArgumentNullException.ThrowIfNull(warehouses);
            ArgumentNullException.ThrowIfNull(products);
            ArgumentNullException.ThrowIfNull(purchaseOrders);
            ArgumentNullException.ThrowIfNull(orderNumbers);
            ArgumentNullException.ThrowIfNull(clock);

            _currentUser = currentUser;
            _suppliers = suppliers;
            _warehouses = warehouses;
            _products = products;
            _purchaseOrders = purchaseOrders;
            _orderNumbers = orderNumbers;
            _clock = clock;
        }

        public async Task<CreatePurchaseOrderResult> HandleAsync(
            CreatePurchaseOrderCommand command,
            CancellationToken cancellationToken = default)
        {
            var order = await CreateDraftAsync(command, cancellationToken);
            var lines = command.Lines.ToArray();
            var productIds = new HashSet<Guid>();

            for (var index = 0; index < lines.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var line = lines[index];
                var field = $"{nameof(command.Lines)}[{index}]";

                if (line is null)
                {
                    throw new RequestValidationException(
                        field, "An order line is required.");
                }

                var productField = $"{field}.{nameof(line.ProductId)}";

                if (line.ProductId == Guid.Empty)
                {
                    throw new RequestValidationException(
                        productField, "Product ID must not be empty.");
                }

                if (!productIds.Add(line.ProductId))
                {
                    throw new RequestValidationException(
                        productField, "A product may appear only once per order.");
                }

                var product = await _products.GetByIdAsync(
                    line.ProductId, cancellationToken)
                    ?? throw new ResourceNotFoundException("Product", line.ProductId);

                if (!product.IsActive)
                {
                    throw new BusinessRuleException("Product is inactive.");
                }

                try
                {
                    order.AddLine(
                        product.Id,
                        product.Sku,
                        product.Name,
                        line.OrderedQuantity,
                        line.UnitPrice);
                }
                catch (ArgumentException exception)
                    when (exception.ParamName is "orderedQuantity" or "unitPrice")
                {
                    var property = exception.ParamName == "orderedQuantity"
                        ? nameof(line.OrderedQuantity)
                        : nameof(line.UnitPrice);

                    throw new RequestValidationException(
                        $"{field}.{property}", exception.Message);
                }
                catch (OverflowException)
                {
                    throw new RequestValidationException(
                        field, "The line or order total exceeds the supported range.");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            await _purchaseOrders.SaveAsync(order, cancellationToken);

            return new CreatePurchaseOrderResult(
                order.Id,
                order.OrderNumber,
                order.Status,
                order.Currency,
                order.TotalAmount,
                order.CreatedAt,
                order.CreatedByUserId);
        }

        private async Task<PurchaseOrder> CreateDraftAsync(
            CreatePurchaseOrderCommand command,
            CancellationToken cancellationToken)
        {
            var creatorId = await AuthorizeAndValidateAsync(
                command,
                cancellationToken);

            var (supplier, warehouse) = await LoadPartiesAsync(
                command,
                cancellationToken);

            return new PurchaseOrder(
                orderNumber: _orderNumbers.Generate(),
                supplierId: supplier.Id,
                supplierCode: supplier.Code,
                supplierName: supplier.Name,
                warehouseId: warehouse.Id,
                warehouseCode: warehouse.Code,
                warehouseName: warehouse.Name,
                createdAt: _clock.UtcNow,
                createdByUserId: creatorId);
        }

        private async Task<Guid> AuthorizeAndValidateAsync(
            CreatePurchaseOrderCommand command,
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
                StockFlowPolicies.PurchaseOrdersManage,
                cancellationToken))
            {
                throw new PermissionDeniedException();
            }

            if (command.SupplierId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.SupplierId),
                    "Supplier ID must not be empty.");
            }

            if (command.WarehouseId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.WarehouseId),
                    "Warehouse ID must not be empty.");
            }

            if (command.Lines is null)
            {
                throw new RequestValidationException(
                    nameof(command.Lines),
                    "Provide a lines collection. It may be empty for a draft.");
            }

            return userId.Value;
        }

        private async Task<(Supplier Supplier, Warehouse Warehouse)> LoadPartiesAsync(
            CreatePurchaseOrderCommand command,
            CancellationToken cancellationToken)
        {
            var supplier = await _suppliers.GetByIdAsync(
                command.SupplierId,
                cancellationToken)
                ?? throw new ResourceNotFoundException("Supplier", command.SupplierId);

            if (!supplier.IsActive)
            {
                throw new BusinessRuleException("Supplier is inactive.");
            }

            var warehouse = await _warehouses.GetByIdAsync(
                command.WarehouseId,
                cancellationToken)
                ?? throw new ResourceNotFoundException("Warehouse", command.WarehouseId);

            if (!warehouse.IsActive)
            {
                throw new BusinessRuleException("Warehouse is inactive.");
            }

            return (supplier, warehouse);
        }
    }
}
