namespace StockFlow.Application.Exceptions
{
    public sealed class InventoryConflictException : Exception
    {
        public Guid ProductId { get; }
        public Guid WarehouseId { get; }

        public InventoryConflictException(
            Guid productId,
            Guid warehouseId)
            : base(
                "Inventory changed during this operation. " +
                "Reload the current balance before trying again.")
        {
            ProductId = productId;
            WarehouseId = warehouseId;
        }
    }
}
