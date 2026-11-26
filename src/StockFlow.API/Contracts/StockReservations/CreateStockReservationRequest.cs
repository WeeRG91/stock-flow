using System.ComponentModel.DataAnnotations;

namespace StockFlow.API.Contracts.StockReservations
{
    public sealed class CreateStockReservationRequest : IValidatableObject
    {
        [Required]
        public Guid? RequestId { get; init; }

        [Required]
        public Guid? InventoryId { get; init; }

        [Required]
        [Range(1, int.MaxValue)]
        public int? Quantity { get; init; }

        [Required]
        public string? Reference { get; init; }

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (RequestId == Guid.Empty)
            {
                yield return new ValidationResult(
                    "Request ID must not be empty.",
                    new[] { nameof(RequestId) });
            }

            if (InventoryId == Guid.Empty)
            {
                yield return new ValidationResult(
                    "Inventory ID must not be empty.",
                    new[] { nameof(InventoryId) });
            }
        }
    }
}
