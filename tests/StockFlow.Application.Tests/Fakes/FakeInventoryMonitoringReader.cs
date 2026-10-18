using StockFlow.Application.Inventory.MonitorLowStock;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Application.Tests.Fakes
{
    public sealed class FakeInventoryMonitoringReader
        : IInventoryMonitoringReader
    {
        public IReadOnlyList<InventoryEntity> Inventories { get; set; } =
            Array.Empty<InventoryEntity>();

        public int CallCount { get; private set; }

        public CancellationToken ReceivedToken { get; private set; }

        public Exception? ReadException { get; set; }

        public Task<IReadOnlyList<InventoryEntity>>
            GetMonitoredInventoriesAsync(
                CancellationToken cancellationToken = default)
        {
            CallCount++;
            ReceivedToken = cancellationToken;

            if (ReadException is not null)
            {
                return Task.FromException<IReadOnlyList<InventoryEntity>>(
                    ReadException);
            }

            return Task.FromResult(Inventories);
        }
    }
}