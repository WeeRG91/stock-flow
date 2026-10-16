namespace StockFlow.Application.Warehouses.GetWarehouses
{
    public sealed record WarehouseDto(
        Guid Id,
        string Code,
        string Name,
        string Location,
        bool IsActive);
}
