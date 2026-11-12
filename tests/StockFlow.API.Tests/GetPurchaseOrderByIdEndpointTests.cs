using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockFlow.Application.Authorization;
using StockFlow.Application.PurchaseOrders;
using StockFlow.Application.PurchaseOrders.GetPurchaseOrders;
using StockFlow.Domain.Enums;
using System.Net;
using System.Net.Http.Json;

namespace StockFlow.API.Tests;

public class GetPurchaseOrderByIdEndpointTests
{
    [Fact]
    public async Task GetById_AsViewer_ReturnsSavedSnapshotsAndVersion()
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = [StockFlowRoles.Viewer];

        var approverId = Guid.NewGuid();
        var details = new PurchaseOrderDetailsDto(
            Guid.NewGuid(), "PO-TEST-001",
            Guid.NewGuid(), "SUP-001", "Original Supplier",
            Guid.NewGuid(), "BRU-01", "Brussels Warehouse",
            PurchaseOrderStatus.Approved, factory.OccurredAt, Guid.NewGuid(),
            factory.OccurredAt.AddMinutes(1),
            factory.OccurredAt.AddMinutes(2),
            null, Guid.NewGuid(), "EUR",
            [
                new PurchaseOrderLineDto(
                    Guid.NewGuid(), Guid.NewGuid(), "DOCK-001",
                    "Original Product", 2, 19.95m)
            ])
        {
            ApprovedByUserId = approverId
        };

        var reader = new RecordingOrderReader { Result = details };
        using var host = CreateHost(factory, reader);
        using var client = host.CreateClient();
        using var response = await client.GetAsync(
            $"/api/purchase-orders/{details.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<PurchaseOrderDetailsDto>();

        Assert.NotNull(result);
        Assert.Equal(approverId, result.ApprovedByUserId);
        Assert.Null(result.RejectedByUserId);
        Assert.Equal(details.Id, reader.RequestedId);
        Assert.Equal(details.Id, result.Id);
        Assert.Equal(details.Version, result.Version);
        Assert.Equal("Original Supplier", result.SupplierName);
        Assert.Equal("Original Product", Assert.Single(result.Lines).ProductName);
        Assert.Equal(39.90m, result.TotalAmount);
    }

    [Fact]
    public async Task GetById_WithMissingOrder_ReturnsNotFound()
    {
        using var factory = new StockFlowApiFactory();
        var reader = new RecordingOrderReader();
        using var host = CreateHost(factory, reader);
        using var client = host.CreateClient();

        var id = Guid.NewGuid();
        using var response = await client.GetAsync($"/api/purchase-orders/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(id, reader.RequestedId);
    }

    [Theory]
    [InlineData(false, StockFlowRoles.Viewer, false, HttpStatusCode.Unauthorized)]
    [InlineData(true, "UnknownRole", false, HttpStatusCode.Forbidden)]
    [InlineData(true, StockFlowRoles.Viewer, true, HttpStatusCode.BadRequest)]
    public async Task GetById_WithRejectedRequest_SkipsLookup(
        bool authenticated,
        string role,
        bool emptyId,
        HttpStatusCode expectedStatus)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.IsAuthenticated = authenticated;
        factory.User.Roles = [role];

        var reader = new RecordingOrderReader();
        using var host = CreateHost(factory, reader);
        using var client = host.CreateClient();

        var id = emptyId ? Guid.Empty : Guid.NewGuid();
        using var response = await client.GetAsync($"/api/purchase-orders/{id}");

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Null(reader.RequestedId);
    }

    private static WebApplicationFactory<Program> CreateHost(
        StockFlowApiFactory factory,
        RecordingOrderReader reader)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPurchaseOrderReadService>();
                services.AddSingleton<IPurchaseOrderReadService>(reader);
            });
        });
    }

    private sealed class RecordingOrderReader : IPurchaseOrderReadService
    {
        public PurchaseOrderDetailsDto? Result { get; init; }
        public Guid? RequestedId { get; private set; }

        public Task<PurchaseOrderDetailsDto?> GetByIdAsync(
            Guid purchaseOrderId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestedId = purchaseOrderId;
            return Task.FromResult(Result);
        }

        public Task<GetPurchaseOrdersResult> GetPageAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "This fake supports purchase-order detail tests only.");
        }
    }
}
