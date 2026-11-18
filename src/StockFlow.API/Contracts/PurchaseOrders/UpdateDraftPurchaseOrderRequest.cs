using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace StockFlow.API.Contracts.PurchaseOrders
{
    public sealed class UpdateDraftPurchaseOrderRequest : IValidatableObject
    {
        private DateOnly? _expectedDeliveryDate;

        public DateOnly? ExpectedDeliveryDate
        {
            get => _expectedDeliveryDate;
            init
            {
                _expectedDeliveryDate = value;
                UpdateExpectedDeliveryDate = true;
            }
        }

        [JsonIgnore]
        public bool UpdateExpectedDeliveryDate { get; private set; }

        [Required]
        public Guid? ExpectedVersion { get; init; }

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
