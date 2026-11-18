using StockFlow.Application.PurchaseOrders.MonitorOverduePurchaseOrders;
using StockFlow.Application.Tests.Fakes;
using StockFlow.Domain.Entities;

namespace StockFlow.Application.Tests;

public sealed class MonitorOverduePurchaseOrdersCommandHandlerTests
{
    [Theory]
    [InlineData(22, 18, false)]
    [InlineData(23, 19, true)]
    public async Task HandleAsync_UsesLuxembourgDateAndSynchronizesDistinctEligibleOrders(
        int utcHour, int expectedLocalDay, bool isOverdue)
    {
        var evaluatedAt = new DateTimeOffset(2026, 11, 18, utcHour, 30, 0, TimeSpan.Zero);
        var dueDate = new DateOnly(2026, 11, 18);
        var approved = CreateOrder(dueDate, approved: true);
        var draft = CreateOrder(dueDate, approved: false);
        var reader = new RecordingReader([approved, approved, draft]);
        var store = new RecordingStore();
        var clock = new FakeClock { UtcNow = evaluatedAt };
        var handler = new MonitorOverduePurchaseOrdersCommandHandler(reader, store, clock);

        var result = await handler.HandleAsync();

        Assert.Equal(new DateOnly(2026, 11, expectedLocalDay), reader.Today);
        Assert.Equal(evaluatedAt, store.EvaluatedAt);
        Assert.Equal(1, store.CallCount);
        Assert.Same(store.Result, result);

        if (isOverdue)
        {
            Assert.Equal(approved.Id, Assert.Single(store.OrderIds));
        }
        else
        {
            Assert.Empty(store.OrderIds);
        }
    }

    private static PurchaseOrder CreateOrder(DateOnly dueDate, bool approved)
    {
        var createdAt = new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);
        var order = new PurchaseOrder(
            "PO-OVERDUE-TEST", Guid.NewGuid(), "SUP-01", "Demo supplier",
            Guid.NewGuid(), "BRU", "Brussels", createdAt, Guid.NewGuid());

        order.AddLine(Guid.NewGuid(), "CHAIR", "Office chair", 2, 25m);
        order.SetExpectedDeliveryDate(dueDate);

        if (approved)
        {
            order.Submit(createdAt.AddMinutes(1));
            order.Approve(createdAt.AddMinutes(2), Guid.NewGuid());
        }

        return order;
    }

    private sealed class RecordingReader(IReadOnlyList<PurchaseOrder> candidates)
        : IOverduePurchaseOrderReader
    {
        public DateOnly Today { get; private set; }

        public Task<IReadOnlyList<PurchaseOrder>> GetCandidatesAsync(
            DateOnly today, CancellationToken cancellationToken = default)
        {
            Today = today;
            return Task.FromResult(candidates);
        }
    }

    private sealed class RecordingStore : IOverduePurchaseOrderAlertStore
    {
        public int CallCount { get; private set; }
        public DateTimeOffset EvaluatedAt { get; private set; }
        public IReadOnlyCollection<Guid> OrderIds { get; private set; } = [];
        public OverduePurchaseOrderAlertSyncResult Result { get; } = new(0, 0, 0);

        public Task<OverduePurchaseOrderAlertSyncResult> SynchronizeAsync(
            IReadOnlyCollection<Guid> overduePurchaseOrderIds,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            EvaluatedAt = evaluatedAt;
            OrderIds = overduePurchaseOrderIds.ToArray();
            return Task.FromResult(Result);
        }
    }
}