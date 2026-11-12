using System.ComponentModel.DataAnnotations;

namespace StockFlow.API.Contracts.PurchaseOrders
{
    public sealed class PurchaseOrderVersionRequest
    {
        [Required]
        public Guid? ExpectedVersion { get; init; }
    }
}
