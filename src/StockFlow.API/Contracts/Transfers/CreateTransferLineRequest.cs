using System.ComponentModel.DataAnnotations;

namespace StockFlow.API.Contracts.Transfers
{
    public sealed class CreateTransferLineRequest
    {
        [Required]
        public Guid? ProductId { get; init; }

        [Required]
        [Range(1, int.MaxValue)]
        public int? Quantity { get; init; }
    }
}
