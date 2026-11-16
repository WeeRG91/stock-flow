using System.ComponentModel.DataAnnotations;

namespace StockFlow.API.Contracts.PurchaseOrders
{
    public sealed class ConfirmGoodsReceiptLineRequest
    {
        [Required]
        public Guid? PurchaseOrderLineId { get; init; }

        [Required]
        [Range(1, int.MaxValue)]
        public int? ReceivedQuantity { get; init; }
    }
}
