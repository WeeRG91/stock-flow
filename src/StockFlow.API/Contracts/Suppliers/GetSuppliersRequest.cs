using System.ComponentModel.DataAnnotations;

namespace StockFlow.API.Contracts.Suppliers
{
    public sealed class GetSuppliersRequest
    {
        [Range(1, int.MaxValue)]
        public int PageNumber { get; init; } = 1;

        [Range(1, 100)]
        public int PageSize { get; init; } = 20;

        public string? Search { get; init; }

        public bool? IsActive { get; init; }
    }
}
