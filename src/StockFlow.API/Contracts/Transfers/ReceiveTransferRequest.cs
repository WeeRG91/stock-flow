using System.ComponentModel.DataAnnotations;

namespace StockFlow.API.Contracts.Transfers
{
    public sealed class ReceiveTransferRequest : IValidatableObject
    {
        [Required]
        public Guid? ReceiptId { get; init; }

        [Required]
        public Guid? ExpectedVersion { get; init; }

        [Required]
        [MinLength(1)]
        public List<ReceiveTransferLineRequest?>? Lines { get; init; }

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
                        new[] { $"Lines[{index}]" });
                }
            }
        }
    }

    public sealed class ReceiveTransferLineRequest
    {
        [Required]
        public Guid? TransferLineId { get; init; }

        [Required]
        [Range(1, int.MaxValue)]
        public int? ReceivedQuantity { get; init; }
    }
}
