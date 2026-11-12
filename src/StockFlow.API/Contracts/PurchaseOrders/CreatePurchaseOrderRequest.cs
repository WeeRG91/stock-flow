using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace StockFlow.API.Contracts.PurchaseOrders
{
    public class CreatePurchaseOrderRequest : IValidatableObject
    {
        [Required]
        public Guid? SupplierId { get; init; }

        [Required]
        public Guid? WarehouseId { get; init; }

        [Required]
        public List<CreatePurchaseOrderLineRequest?>? Lines { get; init; }

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (Lines is null)
            {
                yield break;
            }

            for (var index = 0; index < Lines.Count; index++)
            {
                if (Lines[index] is null)
                {
                    yield return new ValidationResult(
                        "An order line is required.",
                        new[] { $"{nameof(Lines)}[{index}]" });
                }
            }
        }
    }
}
