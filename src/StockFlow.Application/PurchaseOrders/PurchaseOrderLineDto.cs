namespace StockFlow.Application.PurchaseOrders
{
    public sealed record PurchaseOrderLineDto(
        Guid Id,
        Guid ProductId,
        string ProductSku,
        string ProductName,
        int OrderedQuantity,
        decimal UnitPrice)
    {
        public decimal LineTotal => checked(OrderedQuantity * UnitPrice);
    }
}
