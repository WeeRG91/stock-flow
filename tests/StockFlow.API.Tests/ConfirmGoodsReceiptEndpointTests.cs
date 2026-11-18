using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Authorization;
using StockFlow.Application.GoodsReceipts.ConfirmGoodsReceipt;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.API.Tests;

public sealed class ConfirmGoodsReceiptEndpointTests
{
    [Theory]
    [InlineData("ready", HttpStatusCode.OK)]
    [InlineData("invalid-quantity", HttpStatusCode.BadRequest)]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("viewer", HttpStatusCode.Forbidden)]
    [InlineData("missing-order", HttpStatusCode.NotFound)]
    [InlineData("stale-order", HttpStatusCode.Conflict)]
    [InlineData("over-receipt", HttpStatusCode.Conflict)]
    [InlineData("save-failure", HttpStatusCode.InternalServerError)]
    public async Task Confirm_ReturnsExpectedResponse(
        string scenario,
        HttpStatusCode expectedStatus)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.IsAuthenticated = scenario != "anonymous";
        factory.User.Roles = scenario == "viewer"
            ? [StockFlowRoles.Viewer]
            : [StockFlowRoles.WarehouseStaff];

        var order = new PurchaseOrder(
            "PO-RECEIPT-API",
            Guid.NewGuid(), "SUP-01", "Demo supplier",
            factory.Warehouse.Id, factory.Warehouse.Code, factory.Warehouse.Name,
            factory.OccurredAt.AddMinutes(-3), Guid.NewGuid());

        var lineId = order.AddLine(
            factory.Product.Id, factory.Product.Sku, factory.Product.Name, 10, 25m);

        order.Submit(factory.OccurredAt.AddMinutes(-2));
        order.Approve(factory.OccurredAt.AddMinutes(-1), Guid.NewGuid());

        var repository = new ReceiptStore(order)
        {
            FailSave = scenario == "save-failure"
        };

        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPurchaseOrderRepository>();
                services.RemoveAll<IGoodsReceiptRepository>();
                services.AddSingleton<IPurchaseOrderRepository>(repository);
                services.AddSingleton<IGoodsReceiptRepository>(repository);
            });
        });

        using var client = scenario == "anonymous"
            ? host.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            })
            : await host.CreateClientWithAntiforgeryAsync();

        var orderId = scenario == "missing-order" ? Guid.NewGuid() : order.Id;
        var path = $"/api/purchase-orders/{orderId}/receipts";
        var receiptId = Guid.NewGuid();

        var body = new
        {
            receiptId,
            expectedVersion = scenario == "stale-order"
                ? Guid.NewGuid()
                : repository.Version,
            lines = new[]
            {
                new
                {
                    purchaseOrderLineId = lineId,
                    receivedQuantity = scenario switch
                    {
                        "viewer" or "invalid-quantity" => 0,
                        "over-receipt" => 11,
                        _ => 4
                    }
                }
            }
        };

        using var response = await client.PostAsJsonAsync(path, body);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(
            scenario is "ready" or "save-failure" ? 1 : 0,
            repository.SaveAttempts);

        if (scenario == "ready")
        {
            var result = await response.Content.ReadFromJsonAsync<ConfirmGoodsReceiptResult>();
            Assert.NotNull(result);
            Assert.Equal(receiptId, result.ReceiptId);
            Assert.Equal(order.Id, result.PurchaseOrderId);
            Assert.Equal(factory.User.Id, result.ConfirmedByUserId);
            Assert.Equal(
                new ConfirmedGoodsReceiptLineDto(lineId, 4),
                Assert.Single(result.Lines));
            Assert.Equal(PurchaseOrderStatus.PartiallyReceived, order.Status);

            // Replay the same body after the fake advances the order version.
            using var replay = await client.PostAsJsonAsync(path, body);
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            Assert.Equal(1, repository.SaveAttempts);
            Assert.Equal(4, Assert.Single(order.Lines).ReceivedQuantity);
        }
        else if (scenario == "save-failure")
        {
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);

            var problemBody = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("internal-store-error", problemBody);
            Assert.Null(repository.ConfirmedReceipt);
        }
    }

    private sealed class ReceiptStore(PurchaseOrder order)
        : IPurchaseOrderRepository, IGoodsReceiptRepository
    {
        public Guid Version { get; private set; } = Guid.NewGuid();
        public int SaveAttempts { get; private set; }
        public bool FailSave { get; init; }
        public GoodsReceipt? ConfirmedReceipt { get; private set; }

        Task<(PurchaseOrder Order, Guid Version)?> IPurchaseOrderRepository.GetByIdAsync(
            Guid purchaseOrderId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            (PurchaseOrder Order, Guid Version)? result =
                purchaseOrderId == order.Id ? (order, Version) : null;
            return Task.FromResult(result);
        }

        Task<GoodsReceipt?> IGoodsReceiptRepository.GetByIdAsync(
            Guid receiptId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                ConfirmedReceipt?.Id == receiptId ? ConfirmedReceipt : null);
        }

        public Task SaveAsync(
            PurchaseOrder purchaseOrder,
            CancellationToken cancellationToken = default,
            IReadOnlyList<PurchasingNotification>? notifications = null)
        {
            throw new InvalidOperationException("Expected a combined confirmation save.");
        }

        public Task SaveConfirmationAsync(
            GoodsReceipt receipt,
            PurchaseOrder purchaseOrder,
            IReadOnlyList<InventoryEntity> inventories,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveAttempts++;

            if (FailSave)
            {
                throw new InvalidOperationException("internal-store-error");
            }

            Assert.Same(order, purchaseOrder);
            Assert.Single(inventories);
            ConfirmedReceipt = receipt;
            Version = Guid.NewGuid();
            return Task.CompletedTask;
        }
    }
}
