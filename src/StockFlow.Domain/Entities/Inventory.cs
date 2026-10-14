using StockFlow.Domain.Enums;

namespace StockFlow.Domain.Entities
{
    public class Inventory
    {
        private readonly List<StockMovement> _pendingMovements = new();

        public IReadOnlyList<StockMovement> PendingMovements => _pendingMovements.AsReadOnly();

        public Guid Id { get; private set; }
        public Guid ProductId { get; private set; }
        public Guid WarehouseId { get; private set; }
        public int QuantityOnHand { get; private set; }

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

            if (quantity > QuantityOnHand)
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
