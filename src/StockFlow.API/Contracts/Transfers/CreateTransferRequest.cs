using System.ComponentModel.DataAnnotations;

namespace StockFlow.API.Contracts.Transfers
{
    public sealed class CreateTransferRequest : IValidatableObject
    {
        [Required]
        public Guid? SourceWarehouseId { get; init; }
        [Required]
        public Guid? DestinationWarehouseId { get; init; }
        [Required]
        public List<CreateTransferLineRequest?>? Lines { get; init; }

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
                        "A transfer line is required.",
                        new[] { $"{nameof(Lines)}[{index}]" });
                }
            }
        }
    }
}
