using System.ComponentModel.DataAnnotations;

namespace StockFlow.API.Contracts.Transfers
{
    public sealed class DispatchTransferRequest
    {
        [Required]
        public Guid? ExpectedVersion { get; init; }
    }
}
