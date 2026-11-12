using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Authorization;
using StockFlow.Domain.Entities;

namespace StockFlow.API.Tests;

public class UpdateDraftPurchaseOrderEndpointTests
{
    [Theory]
    [InlineData(false, HttpStatusCode.NoContent)]
    [InlineData(true, HttpStatusCode.Conflict)]
    public async Task UpdateDraft_UsesExpectedVersion(
        bool stale,
        HttpStatusCode expectedStatus)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = [StockFlowRoles.PurchasingOfficer];

        var order = CreateOrder(factory);
        var original = Assert.Single(order.Lines);
        var repository = new RecordingEditRepository(order);

        using var host = CreateHost(factory, repository);
        using var client = await host.CreateClientWithAntiforgeryAsync();

        var version = stale ? Guid.NewGuid() : repository.Version;
        using var response = await client.PutAsJsonAsync(
            $"/api/purchase-orders/{order.Id}",
            CreateBody(version, factory.Product.Id));

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(1, repository.LookupCallCount);

        if (stale)
        {
            Assert.Equal(0, repository.SaveCallCount);
            Assert.Same(original, Assert.Single(order.Lines));
        }
        else
        {
            Assert.Equal(1, repository.SaveCallCount);
            var saved = Assert.Single(order.Lines);
            Assert.Equal(original.Id, saved.Id);
            Assert.Equal(2, saved.OrderedQuantity);
            Assert.Equal(24m, saved.LineTotal);
        }
    }

    [Fact]
    public async Task UpdateDraft_AsViewer_ReturnsForbiddenBeforeLookup()
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = [StockFlowRoles.Viewer];

        var order = CreateOrder(factory);
        var repository = new RecordingEditRepository(order);

        using var host = CreateHost(factory, repository);
        using var client = await host.CreateClientWithAntiforgeryAsync();

        using var response = await client.PutAsJsonAsync(
            $"/api/purchase-orders/{order.Id}",
            CreateBody(repository.Version, factory.Product.Id));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, repository.LookupCallCount);
        Assert.Equal(0, repository.SaveCallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateDraft_WithInvalidBody_ReturnsBadRequestBeforeLookup(
        bool missingVersion)
    {
        using var factory = new StockFlowApiFactory();
        var order = CreateOrder(factory);
        var repository = new RecordingEditRepository(order);

        using var host = CreateHost(factory, repository);
        using var client = await host.CreateClientWithAntiforgeryAsync();

        object body = missingVersion
            ? new { lines = Array.Empty<object>() }
            : new { expectedVersion = repository.Version, lines = new object?[] { null } };

        using var response = await client.PutAsJsonAsync(
            $"/api/purchase-orders/{order.Id}", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, repository.LookupCallCount);
        Assert.Equal(0, repository.SaveCallCount);
    }

    private static PurchaseOrder CreateOrder(StockFlowApiFactory factory)
    {
        var order = new PurchaseOrder(
            "PO-EDIT-API",
            Guid.NewGuid(), "SUP-001", "Supplier",
            factory.Warehouse.Id, factory.Warehouse.Code, factory.Warehouse.Name,
            factory.OccurredAt, factory.User.Id);

        order.AddLine(
            factory.Product.Id, factory.Product.Sku, factory.Product.Name, 1, 10m);

        return order;
    }

    private static object CreateBody(Guid version, Guid productId) => new
    {
        expectedVersion = version,
        lines = new[] { new { productId, orderedQuantity = 2, unitPrice = 12m } }
    };

    private static WebApplicationFactory<Program> CreateHost(
        StockFlowApiFactory factory,
        RecordingEditRepository repository)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPurchaseOrderRepository>();
                services.AddSingleton<IPurchaseOrderRepository>(repository);
            });
        });
    }

    private sealed class RecordingEditRepository(PurchaseOrder order)
        : IPurchaseOrderRepository
    {
        public Guid Version { get; } = Guid.NewGuid();
        public int LookupCallCount { get; private set; }
        public int SaveCallCount { get; private set; }

        public Task<(PurchaseOrder Order, Guid Version)?> GetByIdAsync(
            Guid purchaseOrderId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LookupCallCount++;

            (PurchaseOrder Order, Guid Version)? loaded =
                purchaseOrderId == order.Id ? (order, Version) : null;

            return Task.FromResult(loaded);
        }

        public Task SaveAsync(
            PurchaseOrder purchaseOrder,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Same(order, purchaseOrder);
            SaveCallCount++;
            return Task.CompletedTask;
        }
    }
}