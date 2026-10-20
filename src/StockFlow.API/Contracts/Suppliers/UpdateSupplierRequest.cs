using System.ComponentModel.DataAnnotations;

namespace StockFlow.API.Contracts.Suppliers
{
    public sealed class UpdateSupplierRequest
    {
        [Required]
        public string? Name { get; init; }
        [Required]
        public string? ContactEmail { get; init; }
        public string? PhoneNumber { get; init; }
    }
}
