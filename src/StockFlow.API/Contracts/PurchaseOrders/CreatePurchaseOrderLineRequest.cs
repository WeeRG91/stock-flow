using System.ComponentModel.DataAnnotations;

namespace StockFlow.API.Contracts.PurchaseOrders
{
    public class CreatePurchaseOrderLineRequest
    {
        [Required]
        public Guid? ProductId { get; init; }

        [Required]
        [Range(1, int.MaxValue)]
        public int? OrderedQuantity { get; init; }

        [Required]
        public decimal? UnitPrice { get; init; }
    }
}
