using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Application.Transfers.ReceiveTransfer;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.API.Tests;

public sealed class ReceiveTransferEndpointTests
{
    [Theory]
    [InlineData("partial", HttpStatusCode.OK)]
    [InlineData("complete", HttpStatusCode.OK)]
    [InlineData("missing-receipt", HttpStatusCode.BadRequest)]
    [InlineData("empty-receipt", HttpStatusCode.BadRequest)]
    [InlineData("missing-version", HttpStatusCode.BadRequest)]
    [InlineData("empty-lines", HttpStatusCode.BadRequest)]
    [InlineData("null-line", HttpStatusCode.BadRequest)]
    [InlineData("zero-quantity", HttpStatusCode.BadRequest)]
    [InlineData("duplicate-line", HttpStatusCode.BadRequest)]
    [InlineData("foreign-line", HttpStatusCode.BadRequest)]
    [InlineData("missing-transfer", HttpStatusCode.NotFound)]
    [InlineData("stale", HttpStatusCode.Conflict)]
    [InlineData("excessive", HttpStatusCode.Conflict)]
    [InlineData("draft", HttpStatusCode.Conflict)]
    [InlineData("receipt-reused", HttpStatusCode.Conflict)]
    [InlineData("viewer", HttpStatusCode.Forbidden)]
    [InlineData("missing-csrf", HttpStatusCode.BadRequest)]
    [InlineData("save-conflict", HttpStatusCode.Conflict)]
    [InlineData("unexpected", HttpStatusCode.InternalServerError)]
    public async Task Receive_EnforcesValidationPermissionsAndPersistenceBehavior(
        string scenario, HttpStatusCode expectedStatus)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = scenario == "viewer"
            ? [StockFlowRoles.Viewer] : [StockFlowRoles.WarehouseStaff];
        var transfer = CreateTransfer(factory, scenario != "draft");
        var originalStatus = transfer.Status;
        var line = Assert.Single(transfer.Lines);
        var version = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var quantity = scenario switch
        {
            "complete" => 5,
            "excessive" => 6,
            "zero-quantity" => 0,
            _ => 2
        };
        var inventory = new InventoryEntity(line.ProductId, transfer.DestinationWarehouseId);
        inventory.ReceiveStock(7, "Opening stock", factory.OccurredAt.AddHours(-1));
        inventory.AcknowledgePersistedMovements(
            inventory.PendingMovements.Select(movement => movement.Id).ToArray());
        factory.Inventories.Inventory = inventory;
        var transfers = new TransferLookup
        {
            Loaded = scenario == "missing-transfer" ? null : (transfer, version)
        };
        var receipts = new ReceiptStore
        {
            Failure = scenario switch
            {
                "save-conflict" => new TransferConflictException(transfer.Id, "Concurrent receipt."),
                "unexpected" => new InvalidOperationException("Sensitive receiving failure detail."),
                _ => null
            }
        };
        if (scenario == "receipt-reused")
        {
            receipts.Existing = new TransferReceipt(receiptId, transfer.Id,
                factory.OccurredAt, factory.User.Id, [new(line.Id, 1)]);
        }

        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITransferRepository>();
                services.RemoveAll<ITransferReceiptRepository>();
                services.AddSingleton<ITransferRepository>(transfers);
                services.AddSingleton<ITransferReceiptRepository>(receipts);
            }));
        using var client = await host.CreateClientWithAntiforgeryAsync();
        if (scenario == "missing-csrf") client.DefaultRequestHeaders.Clear();

        var requestedLine = new
        {
            transferLineId = scenario == "foreign-line" ? Guid.NewGuid() : line.Id,
            receivedQuantity = quantity
        };
        var body = new Dictionary<string, object?>
        {
            ["lines"] = scenario switch
            {
                "empty-lines" => Array.Empty<object>(),
                "null-line" => new object?[] { null },
                "duplicate-line" => new object[] { requestedLine, requestedLine },
                _ => new object[] { requestedLine }
            }
        };
        if (scenario != "missing-receipt")
            body["receiptId"] = scenario == "empty-receipt" ? Guid.Empty : receiptId;
        if (scenario != "missing-version")
            body["expectedVersion"] = scenario == "stale" ? Guid.NewGuid() : version;

        using var response = await client.PostAsJsonAsync(
            $"/api/transfers/{transfer.Id}/receipts", body);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(0, factory.Inventories.SaveCallCount);
        if (scenario is "partial" or "complete" or "save-conflict" or "unexpected")
        {
            Assert.Equal(1, receipts.SaveCount);
            Assert.Equal(quantity, line.ReceivedQuantity);
            Assert.Equal(7 + quantity, inventory.QuantityOnHand);
            var movement = Assert.Single(inventory.PendingMovements);
            Assert.Equal(quantity, movement.QuantityChange);
            Assert.Equal(StockMovementType.Receipt, movement.Type);
            Assert.Equal(factory.OccurredAt, movement.OccurredAt);
            Assert.Equal(quantity == 5 ? TransferStatus.Received : TransferStatus.PartiallyReceived,
                transfer.Status);
            if (expectedStatus == HttpStatusCode.OK)
            {
                var result = await response.Content.ReadFromJsonAsync<ReceiveTransferResult>();
                Assert.NotNull(result);
                Assert.Equal(receiptId, result.ReceiptId);
                Assert.Equal(transfer.Id, result.TransferId);
                Assert.Equal(factory.OccurredAt, result.ConfirmedAt);
                Assert.Equal(factory.User.Id, result.ConfirmedByUserId);
                var resultLine = Assert.Single(result.Lines);
                Assert.Equal(line.Id, resultLine.TransferLineId);
                Assert.Equal(quantity, resultLine.ReceivedQuantity);
                Assert.Same(transfer, receipts.SavedTransfer);
                Assert.Same(inventory, Assert.Single(receipts.SavedInventories));
                Assert.Same(receipts.SavedReceipt, Assert.Single(transfer.PendingReceipts));
            }
            else
            {
                Assert.Null(receipts.SavedReceipt);
                Assert.Single(transfer.PendingReceipts);
            }
        }
        else
        {
            Assert.Equal(0, receipts.SaveCount);
            Assert.Equal(originalStatus, transfer.Status);
            Assert.Equal(0, line.ReceivedQuantity);
            Assert.Equal(7, inventory.QuantityOnHand);
            Assert.Empty(inventory.PendingMovements);
            Assert.Empty(transfer.PendingReceipts);
        }

        if (expectedStatus != HttpStatusCode.OK)
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.NotNull(problem);
            Assert.Equal((int)expectedStatus, problem.Status);
            Assert.DoesNotContain("Sensitive receiving failure detail.",
                await response.Content.ReadAsStringAsync());
        }
        if (scenario is "viewer" or "missing-csrf" or "missing-receipt"
            or "empty-receipt" or "missing-version" or "empty-lines"
            or "null-line" or "zero-quantity" or "duplicate-line")
        {
            Assert.Equal(0, receipts.LookupCount);
            Assert.Equal(0, transfers.LookupCount);
        }
    }

    [Fact]
    public async Task Receive_IdenticalRetryReturnsOriginalConfirmationWithoutAnotherSave()
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = [StockFlowRoles.WarehouseStaff];
        var transfer = CreateTransfer(factory);
        var line = Assert.Single(transfer.Lines);
        var version = Guid.NewGuid();
        var receipts = new ReceiptStore();
        var transfers = new TransferLookup { Loaded = (transfer, version) };
        // No destination balance exists: receiving must create it.
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITransferRepository>();
                services.RemoveAll<ITransferReceiptRepository>();
                services.AddSingleton<ITransferRepository>(transfers);
                services.AddSingleton<ITransferReceiptRepository>(receipts);
            }));
        using var client = await host.CreateClientWithAntiforgeryAsync();
        var body = new
        {
            receiptId = Guid.NewGuid(), expectedVersion = version,
            lines = new[] { new { transferLineId = line.Id, receivedQuantity = 5 } }
        };
        var endpoint = $"/api/transfers/{transfer.Id}/receipts";
        using var first = await client.PostAsJsonAsync(endpoint, body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var original = await first.Content.ReadFromJsonAsync<ReceiveTransferResult>();
        Assert.NotNull(original);
        var balance = Assert.Single(receipts.SavedInventories);
        var inventoryLookups = factory.Inventories.GetCallCount;

        // A changed transfer version must not prevent an exact receipt replay.
        transfers.Loaded = (transfer, Guid.NewGuid());
        using var retry = await client.PostAsJsonAsync(endpoint, body);

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var replay = await retry.Content.ReadFromJsonAsync<ReceiveTransferResult>();
        Assert.NotNull(replay);
        Assert.Equal(original.ReceiptId, replay.ReceiptId);
        Assert.Equal(original.TransferId, replay.TransferId);
        Assert.Equal(original.ConfirmedAt, replay.ConfirmedAt);
        Assert.Equal(original.ConfirmedByUserId, replay.ConfirmedByUserId);
        Assert.Equal(original.Lines.ToArray(), replay.Lines.ToArray());
        Assert.Equal(1, receipts.SaveCount);
        Assert.Equal(1, transfers.LookupCount);
        Assert.Equal(inventoryLookups, factory.Inventories.GetCallCount);
        Assert.Equal(5, balance.QuantityOnHand);
        Assert.Single(balance.PendingMovements);
        Assert.Equal(TransferStatus.Received, transfer.Status);
        Assert.Equal(0, line.InTransitQuantity);
    }

    private static Transfer CreateTransfer(StockFlowApiFactory factory, bool dispatch = true)
    {
        var transfer = new Transfer(factory.Warehouse.Id, factory.Warehouse.Code,
            factory.Warehouse.Name, Guid.NewGuid(), "LIE", "Liège",
            factory.OccurredAt.AddHours(-1), factory.User.Id);
        transfer.AddLine(factory.Product.Id, factory.Product.Sku, factory.Product.Name, 5);
        if (dispatch) transfer.Dispatch(factory.OccurredAt.AddMinutes(-1), factory.User.Id);
        return transfer;
    }

    private sealed class ReceiptStore : ITransferReceiptRepository
    {
        public TransferReceipt? Existing { get; set; }
        public Exception? Failure { get; init; }
        public int LookupCount { get; private set; }
        public int SaveCount { get; private set; }
        public TransferReceipt? SavedReceipt { get; private set; }
        public Transfer? SavedTransfer { get; private set; }
        public IReadOnlyList<InventoryEntity> SavedInventories { get; private set; } = [];

        public Task<TransferReceipt?> GetByIdAsync(Guid receiptId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LookupCount++;
            return Task.FromResult(Existing?.Id == receiptId ? Existing : null);
        }

        public Task SaveConfirmationAsync(TransferReceipt receipt, Transfer transfer,
            IReadOnlyList<InventoryEntity> destinationInventories,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCount++;
            if (Failure is { } failure) return Task.FromException(failure);
            SavedReceipt = receipt;
            SavedTransfer = transfer;
            SavedInventories = destinationInventories.ToArray();
            Existing = receipt;
            return Task.CompletedTask;
        }
    }

    private sealed class TransferLookup : ITransferRepository
    {
        public (Transfer Transfer, Guid Version)? Loaded { get; set; }
        public int LookupCount { get; private set; }

        public Task<(Transfer Transfer, Guid Version)?> GetByIdAsync(Guid transferId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LookupCount++;
            return Task.FromResult(Loaded is { } entry && entry.Transfer.Id == transferId ? Loaded : null);
        }

        public Task SaveDraftAsync(Transfer transfer, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Receiving must use its coordinated save.");
        public Task SaveCancellationAsync(Transfer transfer, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Receiving must use its coordinated save.");
        public Task SaveDispatchAsync(Transfer transfer, IReadOnlyList<InventoryEntity> sourceInventories,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Receiving must use its coordinated save.");
    }
}
