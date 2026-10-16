using StockFlow.Application.Inventory.GetInventory;

namespace StockFlow.API.Tests.Fakes
{
    public sealed class TestInventoryReadService : IInventoryReadService
    {
        public IReadOnlyList<InventoryBalanceDto> Items { get; set; } =
            Array.Empty<InventoryBalanceDto>();

        public int TotalCount { get; set; }
        public Exception? ReadException { get; set; }

        public int CallCount { get; private set; }
        public int ReceivedPageNumber { get; private set; }
        public int ReceivedPageSize { get; private set; }

        public Task<GetInventoryResult> GetInventoryAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;
            ReceivedPageNumber = pageNumber;
            ReceivedPageSize = pageSize;

            if (ReadException is not null)
            {
                throw ReadException;
            }

            return Task.FromResult(new GetInventoryResult(
                Items,
                pageNumber,
                pageSize,
                TotalCount));
        }
    }
}