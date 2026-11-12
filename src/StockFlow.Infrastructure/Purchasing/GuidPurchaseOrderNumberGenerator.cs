using StockFlow.Application.Abstractions.Purchasing;

namespace StockFlow.Infrastructure.Purchasing
{
    public sealed class GuidPurchaseOrderNumberGenerator
        : IPurchaseOrderNumberGenerator
    {
        public string Generate()
        {
            return $"PO-{Guid.NewGuid():N}".ToUpperInvariant();
        }
    }
}