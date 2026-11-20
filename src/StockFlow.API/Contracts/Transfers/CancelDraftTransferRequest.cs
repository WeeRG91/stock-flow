using System.ComponentModel.DataAnnotations;

namespace StockFlow.API.Contracts.Transfers
{
    public sealed class CancelDraftTransferRequest
    {
        [Required]
        public Guid? ExpectedVersion { get; init; }
    }
}
