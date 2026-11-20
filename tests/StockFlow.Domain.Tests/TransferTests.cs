using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.Domain.Tests;

public sealed class TransferTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Dispatch_MovesAllPlannedUnitsIntoTransit()
    {
        var transfer = CreateTransfer();
        transfer.AddLine(Guid.NewGuid(), "CHAIR", "Office chair", 10);
        var actorId = Guid.NewGuid();
        var dispatchedAt = CreatedAt.AddMinutes(1);

        transfer.Dispatch(dispatchedAt, actorId);

        var line = Assert.Single(transfer.Lines);
        Assert.Equal(TransferStatus.Dispatched, transfer.Status);
        Assert.Equal(dispatchedAt, transfer.DispatchedAt);
        Assert.Equal(actorId, transfer.DispatchedByUserId);
        Assert.Equal(10, line.DispatchedQuantity);
        Assert.Equal(0, line.ReceivedQuantity);
        Assert.Equal(10, line.InTransitQuantity);
    }

    [Fact]
    public void ApplyReceipt_WithRepeatedPartialReceipt_PreservesState()
    {
        var transfer = CreateTransfer();
        var lineId = transfer.AddLine(Guid.NewGuid(), "CHAIR", "Office chair", 10);
        transfer.Dispatch(CreatedAt.AddMinutes(1), Guid.NewGuid());
        var receipt = CreateReceipt(transfer, lineId, 4);

        transfer.ApplyReceipt(receipt);

        Assert.Throws<InvalidOperationException>(() => transfer.ApplyReceipt(receipt));

        var line = Assert.Single(transfer.Lines);
        Assert.Equal(4, line.ReceivedQuantity);
        Assert.Equal(6, line.InTransitQuantity);
        Assert.Equal(TransferStatus.PartiallyReceived, transfer.Status);
        Assert.Null(transfer.ReceivedAt);
        Assert.Same(receipt, Assert.Single(transfer.PendingReceipts));
    }

    [Fact]
    public void ApplyReceipt_WithFinalDelivery_CompletesTransfer()
    {
        var transfer = CreateTransfer();
        var lineId = transfer.AddLine(Guid.NewGuid(), "CHAIR", "Office chair", 10);
        transfer.Dispatch(CreatedAt.AddMinutes(1), Guid.NewGuid());
        transfer.ApplyReceipt(CreateReceipt(transfer, lineId, 4));
        var finalReceipt = CreateReceipt(transfer, lineId, 6);

        transfer.ApplyReceipt(finalReceipt);

        var line = Assert.Single(transfer.Lines);
        Assert.Equal(10, line.ReceivedQuantity);
        Assert.Equal(0, line.InTransitQuantity);
        Assert.Equal(TransferStatus.Received, transfer.Status);
        Assert.Equal(finalReceipt.ConfirmedAt, transfer.ReceivedAt);
        Assert.Equal(2, transfer.PendingReceipts.Count);
    }

    [Fact]
    public void Dispatch_WithNoLines_PreservesDraft()
    {
        var transfer = CreateTransfer();

        Assert.Throws<InvalidOperationException>(
            () => transfer.Dispatch(CreatedAt.AddMinutes(1), Guid.NewGuid()));

        Assert.Equal(TransferStatus.Draft, transfer.Status);
        Assert.Null(transfer.DispatchedAt);
        Assert.Null(transfer.DispatchedByUserId);
    }

    [Fact]
    public void Cancel_WithDraft_PreservesLinesAndRecordsActor()
    {
        var transfer = CreateTransfer();
        transfer.AddLine(Guid.NewGuid(), "CHAIR", "Office chair", 10);
        var originalLine = Assert.Single(transfer.Lines);
        var cancelledAt = CreatedAt.AddMinutes(1);
        var actorId = Guid.NewGuid();

        transfer.Cancel(cancelledAt, actorId);

        Assert.Equal(TransferStatus.Cancelled, transfer.Status);
        Assert.Equal(cancelledAt, transfer.CancelledAt);
        Assert.Equal(actorId, transfer.CancelledByUserId);
        Assert.Same(originalLine, Assert.Single(transfer.Lines));
        Assert.Equal(0, originalLine.InTransitQuantity);
        Assert.Null(transfer.DispatchedAt);
    }

    [Fact]
    public void Cancel_AfterDispatch_PreservesDispatchedState()
    {
        var transfer = CreateTransfer();
        transfer.AddLine(Guid.NewGuid(), "CHAIR", "Office chair", 10);
        var dispatchedAt = CreatedAt.AddMinutes(1);
        var dispatcherId = Guid.NewGuid();
        transfer.Dispatch(dispatchedAt, dispatcherId);

        Assert.Throws<InvalidOperationException>(
            () => transfer.Cancel(CreatedAt.AddMinutes(2), Guid.NewGuid()));

        Assert.Equal(TransferStatus.Dispatched, transfer.Status);
        Assert.Equal(dispatchedAt, transfer.DispatchedAt);
        Assert.Equal(dispatcherId, transfer.DispatchedByUserId);
        Assert.Equal(10, Assert.Single(transfer.Lines).InTransitQuantity);
        Assert.Null(transfer.CancelledAt);
        Assert.Null(transfer.CancelledByUserId);
    }

    [Fact]
    public void Dispatch_WhenAlreadyDispatched_PreservesOriginalDispatch()
    {
        var transfer = CreateTransfer();
        transfer.AddLine(Guid.NewGuid(), "CHAIR", "Office chair", 10);
        var dispatchedAt = CreatedAt.AddMinutes(1);
        var dispatcherId = Guid.NewGuid();
        transfer.Dispatch(dispatchedAt, dispatcherId);

        Assert.Throws<InvalidOperationException>(
            () => transfer.Dispatch(CreatedAt.AddMinutes(2), Guid.NewGuid()));

        Assert.Equal(dispatchedAt, transfer.DispatchedAt);
        Assert.Equal(dispatcherId, transfer.DispatchedByUserId);
        Assert.Equal(10, Assert.Single(transfer.Lines).DispatchedQuantity);
        Assert.Equal(TransferStatus.Dispatched, transfer.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ApplyReceipt_WithInvalidSecondLine_PreservesAllState(
    bool exceedsInTransitQuantity)
    {
        var transfer = CreateTransfer();
        var chairId = transfer.AddLine(Guid.NewGuid(), "CHAIR", "Office chair", 10);
        var deskId = transfer.AddLine(Guid.NewGuid(), "DESK", "Office desk", 5);
        transfer.Dispatch(CreatedAt.AddMinutes(1), Guid.NewGuid());

        var invalidLine = exceedsInTransitQuantity
            ? new TransferReceiptLine(deskId, 6)
            : new TransferReceiptLine(Guid.NewGuid(), 1);

        var receipt = new TransferReceipt(
            Guid.NewGuid(),
            transfer.Id,
            CreatedAt.AddMinutes(2),
            Guid.NewGuid(),
            new[]
            {
            new TransferReceiptLine(chairId, 4),
            invalidLine
            });

        if (exceedsInTransitQuantity)
        {
            Assert.Throws<InvalidOperationException>(
                () => transfer.ApplyReceipt(receipt));
        }
        else
        {
            Assert.Throws<ArgumentException>(
                "receipt", () => transfer.ApplyReceipt(receipt));
        }

        Assert.Equal(TransferStatus.Dispatched, transfer.Status);
        Assert.Null(transfer.ReceivedAt);
        Assert.Empty(transfer.PendingReceipts);

        Assert.All(transfer.Lines, line =>
        {
            Assert.Equal(0, line.ReceivedQuantity);
            Assert.Equal(line.DispatchedQuantity, line.InTransitQuantity);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddLine_WithNonPositiveQuantity_PreservesEmptyDraft(int quantity)
    {
        var transfer = CreateTransfer();

        Assert.Throws<ArgumentOutOfRangeException>(
            "quantity",
            () => transfer.AddLine(
                Guid.NewGuid(), "CHAIR", "Office chair", quantity));

        Assert.Empty(transfer.Lines);
        Assert.Equal(TransferStatus.Draft, transfer.Status);
    }

    [Fact]
    public void AddLine_WithDuplicateProduct_PreservesOriginalLine()
    {
        var transfer = CreateTransfer();
        var productId = Guid.NewGuid();
        transfer.AddLine(productId, "CHAIR", "Office chair", 10);
        var originalLine = Assert.Single(transfer.Lines);

        Assert.Throws<InvalidOperationException>(
            () => transfer.AddLine(productId, "CHAIR", "Office chair", 5));

        Assert.Same(originalLine, Assert.Single(transfer.Lines));
        Assert.Equal(10, originalLine.Quantity);
    }

    [Fact]
    public void AddLine_AfterDispatch_PreservesExistingLines()
    {
        var transfer = CreateTransfer();
        transfer.AddLine(Guid.NewGuid(), "CHAIR", "Office chair", 10);
        var originalLine = Assert.Single(transfer.Lines);
        transfer.Dispatch(CreatedAt.AddMinutes(1), Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(
            () => transfer.AddLine(
                Guid.NewGuid(), "DESK", "Office desk", 2));

        Assert.Same(originalLine, Assert.Single(transfer.Lines));
        Assert.Equal(TransferStatus.Dispatched, transfer.Status);
    }

    [Fact]
    public void Constructor_WithValidInput_CreatesNormalizedEmptyDraft()
    {
        var sourceId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();

        var transfer = new Transfer(
            sourceId, " bru ", " Brussels ",
            destinationId, " lie ", " Liège ",
            CreatedAt, creatorId);

        Assert.NotEqual(Guid.Empty, transfer.Id);
        Assert.Equal(sourceId, transfer.SourceWarehouseId);
        Assert.Equal(destinationId, transfer.DestinationWarehouseId);
        Assert.Equal("BRU", transfer.SourceWarehouseCode);
        Assert.Equal("Brussels", transfer.SourceWarehouseName);
        Assert.Equal("LIE", transfer.DestinationWarehouseCode);
        Assert.Equal("Liège", transfer.DestinationWarehouseName);
        Assert.Equal(CreatedAt, transfer.CreatedAt);
        Assert.Equal(creatorId, transfer.CreatedByUserId);
        Assert.Equal(TransferStatus.Draft, transfer.Status);
        Assert.Empty(transfer.Lines);
        Assert.Empty(transfer.PendingReceipts);
        Assert.Null(transfer.DispatchedAt);
        Assert.Null(transfer.ReceivedAt);
        Assert.Null(transfer.CancelledAt);
    }

    [Fact]
    public void Constructor_WithSameWarehouses_RejectsDestination()
    {
        var warehouseId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(
            "destinationWarehouseId",
            () => new Transfer(
                warehouseId, "BRU", "Brussels",
                warehouseId, "BRU", "Brussels",
                CreatedAt, Guid.NewGuid()));
    }

    [Fact]
    public void Lines_RejectsExternalCollectionMutation()
    {
        var transfer = CreateTransfer();
        transfer.AddLine(Guid.NewGuid(), "CHAIR", "Office chair", 10);
        var originalLine = Assert.Single(transfer.Lines);

        var collection = Assert.IsAssignableFrom<ICollection<TransferLine>>(
            transfer.Lines);

        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Clear());

        Assert.Same(originalLine, Assert.Single(transfer.Lines));
    }

    [Fact]
    public void PendingReceipts_RejectsExternalCollectionMutation()
    {
        var transfer = CreateTransfer();
        var lineId = transfer.AddLine(Guid.NewGuid(), "CHAIR", "Office chair", 10);
        transfer.Dispatch(CreatedAt.AddMinutes(1), Guid.NewGuid());
        var receipt = CreateReceipt(transfer, lineId, 4);
        transfer.ApplyReceipt(receipt);

        var collection = Assert.IsAssignableFrom<ICollection<TransferReceipt>>(
            transfer.PendingReceipts);

        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Clear());

        Assert.Same(receipt, Assert.Single(transfer.PendingReceipts));
        Assert.Equal(4, Assert.Single(transfer.Lines).ReceivedQuantity);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ApplyReceipt_WithWrongTransferOrTimeBeforeDispatch_PreservesState(
    bool wrongTransfer)
    {
        var transfer = CreateTransfer();
        var lineId = transfer.AddLine(Guid.NewGuid(), "CHAIR", "Office chair", 10);
        var dispatchedAt = CreatedAt.AddMinutes(1);
        transfer.Dispatch(dispatchedAt, Guid.NewGuid());

        var receipt = new TransferReceipt(
            Guid.NewGuid(),
            wrongTransfer ? Guid.NewGuid() : transfer.Id,
            wrongTransfer ? dispatchedAt.AddMinutes(1) : CreatedAt,
            Guid.NewGuid(),
            new[] { new TransferReceiptLine(lineId, 4) });

        Assert.Throws<ArgumentException>(
            "receipt", () => transfer.ApplyReceipt(receipt));

        var line = Assert.Single(transfer.Lines);
        Assert.Equal(0, line.ReceivedQuantity);
        Assert.Equal(10, line.InTransitQuantity);
        Assert.Equal(TransferStatus.Dispatched, transfer.Status);
        Assert.Null(transfer.ReceivedAt);
        Assert.Empty(transfer.PendingReceipts);
    }

    [Fact]
    public void ApplyReceipt_CompletesTransferOnlyWhenEveryLineHasArrived()
    {
        var transfer = CreateTransfer();
        var chairId = transfer.AddLine(
            Guid.NewGuid(), "CHAIR", "Office chair", 2);
        var deskId = transfer.AddLine(
            Guid.NewGuid(), "DESK", "Office desk", 3);

        transfer.Dispatch(CreatedAt.AddMinutes(1), Guid.NewGuid());
        transfer.ApplyReceipt(CreateReceipt(transfer, chairId, 2));

        var chair = Assert.Single(transfer.Lines, line => line.Id == chairId);
        var desk = Assert.Single(transfer.Lines, line => line.Id == deskId);

        Assert.Equal(0, chair.InTransitQuantity);
        Assert.Equal(3, desk.InTransitQuantity);
        Assert.Equal(TransferStatus.PartiallyReceived, transfer.Status);
        Assert.Null(transfer.ReceivedAt);

        var finalReceipt = CreateReceipt(transfer, deskId, 3);
        transfer.ApplyReceipt(finalReceipt);

        Assert.All(transfer.Lines, line => Assert.Equal(0, line.InTransitQuantity));
        Assert.Equal(TransferStatus.Received, transfer.Status);
        Assert.Equal(finalReceipt.ConfirmedAt, transfer.ReceivedAt);
        Assert.Equal(2, transfer.PendingReceipts.Count);
    }

    [Theory]
    [InlineData("dispatch", "default-time")]
    [InlineData("dispatch", "non-utc")]
    [InlineData("dispatch", "before-creation")]
    [InlineData("dispatch", "empty-user")]
    [InlineData("cancel", "default-time")]
    [InlineData("cancel", "non-utc")]
    [InlineData("cancel", "before-creation")]
    [InlineData("cancel", "empty-user")]
    public void LifecycleOperation_WithInvalidInput_PreservesDraft(
    string operation, string scenario)
    {
        var transfer = CreateTransfer();
        transfer.AddLine(Guid.NewGuid(), "CHAIR", "Office chair", 10);

        var timestamp = scenario switch
        {
            "default-time" => default,
            "non-utc" => CreatedAt.AddMinutes(1).ToOffset(TimeSpan.FromHours(1)),
            "before-creation" => CreatedAt.AddSeconds(-1),
            _ => CreatedAt.AddMinutes(1)
        };
        var actorId = scenario == "empty-user" ? Guid.Empty : Guid.NewGuid();
        var parameterName = operation == "dispatch"
            ? scenario == "empty-user" ? "dispatchedByUserId" : "dispatchedAt"
            : scenario == "empty-user" ? "cancelledByUserId" : "cancelledAt";

        Action action = operation == "dispatch"
            ? () => transfer.Dispatch(timestamp, actorId)
            : () => transfer.Cancel(timestamp, actorId);

        var exception = Assert.ThrowsAny<ArgumentException>(action);
        Assert.Equal(parameterName, exception.ParamName);

        Assert.Equal(TransferStatus.Draft, transfer.Status);
        Assert.Null(transfer.DispatchedAt);
        Assert.Null(transfer.DispatchedByUserId);
        Assert.Null(transfer.CancelledAt);
        Assert.Null(transfer.CancelledByUserId);

        var line = Assert.Single(transfer.Lines);
        Assert.Equal(0, line.DispatchedQuantity);
        Assert.Equal(0, line.InTransitQuantity);
    }

    [Theory]
    [InlineData(TransferStatus.Draft)]
    [InlineData(TransferStatus.Cancelled)]
    [InlineData(TransferStatus.Received)]
    public void ApplyReceipt_WithIneligibleStatus_PreservesState(
    TransferStatus status)
    {
        var transfer = CreateTransfer();
        var lineId = transfer.AddLine(
            Guid.NewGuid(), "CHAIR", "Office chair", 10);

        if (status == TransferStatus.Cancelled)
        {
            transfer.Cancel(CreatedAt.AddMinutes(1), Guid.NewGuid());
        }
        else if (status == TransferStatus.Received)
        {
            transfer.Dispatch(CreatedAt.AddMinutes(1), Guid.NewGuid());
            transfer.ApplyReceipt(CreateReceipt(transfer, lineId, 10));
        }

        var line = Assert.Single(transfer.Lines);
        var receivedBefore = line.ReceivedQuantity;
        var inTransitBefore = line.InTransitQuantity;
        var completedAtBefore = transfer.ReceivedAt;
        var receiptCountBefore = transfer.PendingReceipts.Count;
        var receipt = CreateReceipt(transfer, lineId, 1);

        Assert.Throws<InvalidOperationException>(
            () => transfer.ApplyReceipt(receipt));

        Assert.Equal(status, transfer.Status);
        Assert.Equal(receivedBefore, line.ReceivedQuantity);
        Assert.Equal(inTransitBefore, line.InTransitQuantity);
        Assert.Equal(completedAtBefore, transfer.ReceivedAt);
        Assert.Equal(receiptCountBefore, transfer.PendingReceipts.Count);
    }

    [Fact]
    public void ReplaceDraftLines_PreservesRetainedIdentityAndSnapshots()
    {
        var transfer = CreateTransfer();
        var retainedProductId = Guid.NewGuid();
        var removedProductId = Guid.NewGuid();
        var newProductId = Guid.NewGuid();

        transfer.AddLine(retainedProductId, "CHAIR", "Original chair", 4);
        transfer.AddLine(removedProductId, "DESK", "Office desk", 2);
        var retained = transfer.Lines[0];

        transfer.ReplaceDraftLines(
        [
            new(retainedProductId, "CHANGED", "Changed name", 6),
        new(newProductId, "LAMP", "Desk lamp", 3)
        ]);

        Assert.Equal(2, transfer.Lines.Count);
        Assert.Same(retained, transfer.Lines[0]);
        Assert.Equal("CHAIR", retained.ProductSku);
        Assert.Equal("Original chair", retained.ProductName);
        Assert.Equal(6, retained.Quantity);
        Assert.DoesNotContain(transfer.Lines, line => line.ProductId == removedProductId);

        var added = Assert.Single(
            transfer.Lines, line => line.ProductId == newProductId);
        Assert.Equal("LAMP", added.ProductSku);
        Assert.Equal(3, added.Quantity);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReplaceDraftLines_WithInvalidLaterLine_PreservesOriginalDraft(
        bool duplicateProduct)
    {
        var transfer = CreateTransfer();
        var productId = Guid.NewGuid();
        transfer.AddLine(productId, "CHAIR", "Office chair", 4);
        var original = Assert.Single(transfer.Lines);

        TransferLineDraft[] drafts =
        [
            new(productId, "CHAIR", "Office chair", 8),
        duplicateProduct
            ? new(productId, "CHAIR", "Office chair", 2)
            : new(Guid.NewGuid(), "", "Invalid product", 2)
        ];

        Assert.Throws<ArgumentException>(
            () => transfer.ReplaceDraftLines(drafts));

        Assert.Same(original, Assert.Single(transfer.Lines));
        Assert.Equal(4, original.Quantity);
        Assert.Equal(TransferStatus.Draft, transfer.Status);
    }

    [Fact]
    public void ReplaceDraftLines_WithEmptyCollection_ClearsDraft()
    {
        var transfer = CreateTransfer();
        transfer.AddLine(Guid.NewGuid(), "CHAIR", "Office chair", 4);

        transfer.ReplaceDraftLines([]);

        Assert.Empty(transfer.Lines);
        Assert.Equal(TransferStatus.Draft, transfer.Status);
    }

    [Fact]
    public void ReplaceDraftLines_AfterDispatch_PreservesLines()
    {
        var transfer = CreateTransfer();
        transfer.AddLine(Guid.NewGuid(), "CHAIR", "Office chair", 4);
        var original = Assert.Single(transfer.Lines);
        transfer.Dispatch(CreatedAt.AddMinutes(1), Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(
            () => transfer.ReplaceDraftLines([]));

        Assert.Same(original, Assert.Single(transfer.Lines));
        Assert.Equal(4, original.InTransitQuantity);
        Assert.Equal(TransferStatus.Dispatched, transfer.Status);
    }

    private static Transfer CreateTransfer()
    {
        return new Transfer(
            Guid.NewGuid(), "BRU", "Brussels",
            Guid.NewGuid(), "LIE", "Liège",
            CreatedAt, Guid.NewGuid());
    }

    private static TransferReceipt CreateReceipt(
        Transfer transfer, Guid lineId, int quantity)
    {
        return new TransferReceipt(
            Guid.NewGuid(), transfer.Id, CreatedAt.AddMinutes(2),
            Guid.NewGuid(),
            new[] { new TransferReceiptLine(lineId, quantity) });
    }
}