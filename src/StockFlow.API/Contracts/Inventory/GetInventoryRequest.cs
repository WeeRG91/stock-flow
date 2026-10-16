using System.ComponentModel.DataAnnotations;

namespace StockFlow.API.Contracts.Inventory
{
    public sealed class GetInventoryRequest
    {
        [Range(1, int.MaxValue)]
        public int PageNumber { get; init; } = 1;

        [Range(1, 100)]
        public int PageSize { get; init; } = 20;
    }
}
