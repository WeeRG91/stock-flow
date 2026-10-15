using System.ComponentModel.DataAnnotations;

namespace StockFlow.API.Contracts.Inventory
{
    public sealed class ReceiveStockRequest : IValidatableObject
    {
        [Required]
        public Guid? ProductId { get; init; }

        [Required]
        public Guid? WarehouseId { get; init; }

        [Required]
        [Range(1, int.MaxValue)]
        public int? Quantity { get; init; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ProductId == Guid.Empty)
            {
                yield return new ValidationResult(
                    "Product ID must not be empty.",
                    new[] { nameof(ProductId) });
            }

            if (WarehouseId == Guid.Empty)
            {
                yield return new ValidationResult(
                    "Warehouse ID must not be empty.",
                    new[] { nameof(WarehouseId) });
            }
        }
    }
}
