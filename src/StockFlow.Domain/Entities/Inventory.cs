using StockFlow.Domain.Enums;

namespace StockFlow.Domain.Entities
{
    public class Inventory
    {
        private readonly List<StockMovement> _pendingMovements = new();
        private int _quantityReserved = 0;

        public IReadOnlyList<StockMovement> PendingMovements => _pendingMovements.AsReadOnly();

        public Guid Id { get; private set; }
        public Guid ProductId { get; private set; }
        public Guid WarehouseId { get; private set; }
        public int QuantityOnHand { get; private set; }

        public int QuantityReserved => _quantityReserved;
        public int QuantityAvailable => QuantityOnHand - QuantityReserved;

        public int? ReorderThreshold { get; private set; }
        public bool IsLowStock =>
            ReorderThreshold.HasValue &&
            QuantityOnHand < ReorderThreshold.Value;

        public void SetReorderThreshold(int threshold)
        {
            if (threshold <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(threshold),
                    "Reorder threshold must be greater than zero.");
            }

            ReorderThreshold = threshold;
        }

        public void DisableLowStockMonitoring()
        {
            ReorderThreshold = null;
        }

        public Inventory(Guid productId, Guid warehouseId)
        {
            if (productId == Guid.Empty)
            {
                throw new ArgumentException(
                "Product ID must not be empty.",
                nameof(productId));
            }

            if (warehouseId == Guid.Empty)
            {
                throw new ArgumentException(
                "Warehouse ID must not be empty.",
                nameof(warehouseId));
            }

            Id = Guid.NewGuid();
            ProductId = productId;
            WarehouseId = warehouseId;
            QuantityOnHand = 0;
        }

        public void ReserveStock(int quantity)
        {
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    "Quantity must be greater than zero.");
            }

            if (quantity > QuantityAvailable)
            {
                throw new InvalidOperationException(
                    "Cannot reserve more stock than is available.");
            }

            var newReservedQuantity = checked(QuantityReserved + quantity);

            _quantityReserved = newReservedQuantity;
        }

        public void ReleaseReservedStock(int quantity)
        {
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    "Quantity must be greater than zero.");
            }

            if (quantity > QuantityReserved)
            {
                throw new InvalidOperationException(
                    "Cannot release more stock than is reserved.");
            }

            var newReservedQuantity = checked(QuantityReserved - quantity);

            _quantityReserved = newReservedQuantity;
        }

        public void ConsumeReservedStock(
            int quantity,
            string reason,
            DateTimeOffset occurredAt)
        {
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    "Quantity must be greater than zero.");
            }

            if (quantity > QuantityReserved)
            {
                throw new InvalidOperationException(
                    "Cannot consume more stock than is reserved.");
            }

            if (occurredAt == default || occurredAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Occurrence time must be a non-default UTC value.",
                    nameof(occurredAt));
            }

            var newQuantityOnHand = checked(QuantityOnHand - quantity);
            var newQuantityReserved = checked(QuantityReserved - quantity);

            var movement = new StockMovement(
                Id,
                -quantity,
                StockMovementType.Removal,
                reason,
                occurredAt);

            _pendingMovements.Add(movement);
            QuantityOnHand = newQuantityOnHand;
            _quantityReserved = newQuantityReserved;
        }

        public void ReceiveStock(
            int quantity,
            string reason,
            DateTimeOffset occurredAt)
        {
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    "Quantity must be greater than zero.");
            }

            if (occurredAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Occurrence timestamp must use UTC.",
                    nameof(occurredAt));
            }

            var newQuantity = checked(QuantityOnHand + quantity);

            var movement = new StockMovement(
                Id,
                quantity,
                StockMovementType.Receipt,
                reason,
                occurredAt);

            _pendingMovements.Add(movement);
            QuantityOnHand = newQuantity;
        }

        public void RemoveStock(
            int quantity,
            string reason,
            DateTimeOffset occurredAt)
        {
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    "Quantity must be greater than zero.");
            }

            if (quantity > QuantityAvailable)
            {
                throw new InvalidOperationException(
                    "Cannot remove more stock than is available.");
            }

            if (occurredAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Occurrence timestamp must use UTC.",
                    nameof(occurredAt));
            }

            var newQuantity = checked(QuantityOnHand - quantity);

            var movement = new StockMovement(
                Id,
                -quantity,
                StockMovementType.Removal,
                reason,
                occurredAt);

            _pendingMovements.Add(movement);
            QuantityOnHand = newQuantity;
        }

        public void AcknowledgePersistedMovements(
            IEnumerable<Guid> movementIds)
        {
            ArgumentNullException.ThrowIfNull(movementIds);

            var persistedIds = movementIds.ToHashSet();

            _pendingMovements.RemoveAll(
                movement => persistedIds.Contains(movement.Id));
        }
    }
}
