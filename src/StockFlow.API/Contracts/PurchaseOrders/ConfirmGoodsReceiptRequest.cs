using System.ComponentModel.DataAnnotations;

namespace StockFlow.API.Contracts.PurchaseOrders
{
    public sealed class ConfirmGoodsReceiptRequest : IValidatableObject
    {
        [Required]
        public Guid? ReceiptId { get; init; }

        [Required]
        public Guid? ExpectedVersion { get; init; }

        [Required]
        [MinLength(1, ErrorMessage = "Provide at least one receipt line.")]
        public List<ConfirmGoodsReceiptLineRequest?>? Lines { get; init; }

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
                        "A receipt line is required.",
                        new[] { $"{nameof(Lines)}[{index}]" });
                }
            }
        }
    }
}
