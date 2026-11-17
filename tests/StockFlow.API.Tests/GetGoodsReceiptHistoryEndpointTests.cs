using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockFlow.Application.Authorization;
using StockFlow.Application.GoodsReceipts;
using StockFlow.Application.GoodsReceipts.GetGoodsReceipts;

namespace StockFlow.API.Tests;

public sealed class GetGoodsReceiptHistoryEndpointTests
{
    [Theory]
    [InlineData("with-receipt", HttpStatusCode.OK)]
    [InlineData("empty-history", HttpStatusCode.OK)]
    [InlineData("missing-order", HttpStatusCode.NotFound)]
    [InlineData("empty-id", HttpStatusCode.BadRequest)]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("forbidden", HttpStatusCode.Forbidden)]
    public async Task GetHistory_ReturnsExpectedResponse(
        string scenario,
        HttpStatusCode expectedStatus)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.IsAuthenticated = scenario != "anonymous";
        factory.User.Roles = scenario == "forbidden"
            ? ["UnknownRole"]
            : [StockFlowRoles.Viewer];

        var orderId = scenario == "empty-id" ? Guid.Empty : Guid.NewGuid();
        var receipt = new GoodsReceiptHistoryItemDto(
            Guid.NewGuid(),
            factory.OccurredAt,
            [new GoodsReceiptHistoryLineDto(Guid.NewGuid(), 4)]);

        var reader = new RecordingHistoryReader(scenario switch
        {
            "with-receipt" => [receipt],
            "missing-order" => null,
            _ => []
        });

        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGoodsReceiptReadService>();
                services.AddSingleton<IGoodsReceiptReadService>(reader);
            });
        });

        using var client = host.CreateClient();
        using var response = await client.GetAsync(
            $"/api/purchase-orders/{orderId}/receipts");

        Assert.Equal(expectedStatus, response.StatusCode);

        if (scenario is "anonymous" or "forbidden" or "empty-id")
        {
            Assert.Null(reader.RequestedOrderId);
            return;
        }

        Assert.Equal(orderId, reader.RequestedOrderId);

        if (expectedStatus == HttpStatusCode.OK)
        {
            var history = await response.Content
                .ReadFromJsonAsync<GoodsReceiptHistoryItemDto[]>();
            Assert.NotNull(history);

            if (scenario == "empty-history")
            {
                Assert.Empty(history);
            }
            else
            {
                var item = Assert.Single(history);
                Assert.Equal(receipt.ReceiptId, item.ReceiptId);
                Assert.Equal(receipt.ConfirmedAt, item.ConfirmedAt);
                Assert.Equal(Assert.Single(receipt.Lines), Assert.Single(item.Lines));
            }
        }
    }

    private sealed class RecordingHistoryReader(
        IReadOnlyList<GoodsReceiptHistoryItemDto>? history) : IGoodsReceiptReadService
    {
        public Guid? RequestedOrderId { get; private set; }

        public Task<IReadOnlyList<GoodsReceiptHistoryItemDto>?> GetByPurchaseOrderIdAsync(
            Guid purchaseOrderId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestedOrderId = purchaseOrderId;
            return Task.FromResult(history);
        }
    }
}