using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockFlow.Application.Abstractions.Notifications;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Authorization;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.API.Tests;

public class PurchaseOrderLifecycleEndpointTests
{
    [Theory]
    [InlineData("ready", HttpStatusCode.NoContent)]
    [InlineData("empty", HttpStatusCode.Conflict)]
    [InlineData("stale", HttpStatusCode.Conflict)]
    [InlineData("viewer", HttpStatusCode.Forbidden)]
    public async Task Submit_EnforcesDraftRulesAndPermission(
        string scenario,
        HttpStatusCode expectedStatus)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = scenario == "viewer"
            ? [StockFlowRoles.Viewer]
            : [StockFlowRoles.PurchasingOfficer];

        var order = new PurchaseOrder(
            "PO-SUBMIT-API",
            Guid.NewGuid(), "SUP-001", "Supplier",
            factory.Warehouse.Id, factory.Warehouse.Code, factory.Warehouse.Name,
            factory.OccurredAt.AddMinutes(-1), factory.User.Id);

        if (scenario != "empty")
        {
            order.AddLine(
                factory.Product.Id, factory.Product.Sku, factory.Product.Name, 1, 10m);
        }

        var repository = new RecordingLifecycleRepository(order);
        var approverId = Guid.NewGuid();
        var recipients = new FixedApproverRecipients(order.CreatedByUserId, approverId);
        var notifier = new RecordingPurchasingNotifier(repository);
        using var host = CreateHost(factory, repository, recipients, notifier);
        using var client = await host.CreateClientWithAntiforgeryAsync();

        var version = scenario == "stale" ? Guid.NewGuid() : repository.Version;
        using var response = await client.PostAsJsonAsync(
            $"/api/purchase-orders/{order.Id}/submit",
            new { expectedVersion = version });

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(scenario == "viewer" ? 0 : 1, repository.LookupCallCount);

        if (expectedStatus == HttpStatusCode.NoContent)
        {
            Assert.Equal(1, repository.SaveCallCount);
            Assert.Equal(PurchaseOrderStatus.PendingApproval, order.Status);
            Assert.Equal(factory.OccurredAt, order.SubmittedAt);
            Assert.Equal(1, recipients.LookupCallCount);
            Assert.Equal(1, notifier.CallCount);
            Assert.Equal(1, notifier.SaveCountAtNotification);
            Assert.Equal(approverId, Assert.Single(notifier.RecipientIds));
            var notification = Assert.Single(repository.SavedNotifications);
            Assert.Equal(approverId, notification.RecipientUserId);
            Assert.Equal(order.Id, notification.PurchaseOrderId);
            Assert.Equal(PurchasingNotificationKind.PurchaseOrderSubmitted, notification.Kind);
            Assert.Equal(factory.OccurredAt, notification.CreatedAt);
            Assert.Null(notification.ReadAt);
        }
        else
        {
            Assert.Equal(0, repository.SaveCallCount);
            Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
            Assert.Equal(0, recipients.LookupCallCount);
            Assert.Equal(0, notifier.CallCount);
            Assert.Empty(repository.SavedNotifications);
            Assert.Null(order.SubmittedAt);
        }
    }

    [Theory]
    [InlineData("ready", HttpStatusCode.NoContent)]
    [InlineData("self", HttpStatusCode.Forbidden)]
    [InlineData("officer", HttpStatusCode.Forbidden)]
    [InlineData("stale", HttpStatusCode.Conflict)]
    [InlineData("draft", HttpStatusCode.Conflict)]
    public async Task Approve_EnforcesPermissionCreatorAndVersion(
    string scenario,
    HttpStatusCode expectedStatus)
    {
        using var factory = new StockFlowApiFactory();

        var role = scenario switch
        {
            "self" => StockFlowRoles.Administrator,
            "officer" => StockFlowRoles.PurchasingOfficer,
            _ => StockFlowRoles.PurchasingManager
        };

        factory.User.Roles = [role];

        var creatorId = scenario == "self" ? factory.User.Id : Guid.NewGuid();
        var order = new PurchaseOrder(
            "PO-APPROVE-API",
            Guid.NewGuid(), "SUP-001", "Supplier",
            factory.Warehouse.Id, factory.Warehouse.Code, factory.Warehouse.Name,
            factory.OccurredAt.AddMinutes(-2), creatorId);

        order.AddLine(
            factory.Product.Id, factory.Product.Sku, factory.Product.Name, 1, 10m);

        if (scenario != "draft")
        {
            order.Submit(factory.OccurredAt.AddMinutes(-1));
        }

        var initialStatus = order.Status;
        var repository = new RecordingLifecycleRepository(order);
        var notifier = new RecordingPurchasingNotifier(repository);
        using var host = CreateHost(factory, repository, notifier: notifier);
        using var client = await host.CreateClientWithAntiforgeryAsync();

        var version = scenario == "stale" ? Guid.NewGuid() : repository.Version;
        using var response = await client.PostAsJsonAsync(
            $"/api/purchase-orders/{order.Id}/approve",
            new { expectedVersion = version });

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(scenario == "officer" ? 0 : 1, repository.LookupCallCount);

        if (expectedStatus == HttpStatusCode.NoContent)
        {
            Assert.Equal(1, repository.SaveCallCount);
            Assert.Equal(PurchaseOrderStatus.Approved, order.Status);
            Assert.Equal(factory.User.Id, order.ApprovedByUserId);
            Assert.Equal(factory.OccurredAt, order.ApprovedAt);
            Assert.Equal(1, notifier.CallCount);
            Assert.Equal(1, notifier.SaveCountAtNotification);
            Assert.Equal(order.CreatedByUserId, Assert.Single(notifier.RecipientIds));
            var notification = Assert.Single(repository.SavedNotifications);
            Assert.Equal(order.CreatedByUserId, notification.RecipientUserId);
            Assert.Equal(order.Id, notification.PurchaseOrderId);
            Assert.Equal(PurchasingNotificationKind.PurchaseOrderApproved, notification.Kind);
            Assert.Equal(factory.OccurredAt, notification.CreatedAt);
            Assert.Null(notification.ReadAt);
        }
        else
        {
            Assert.Equal(0, repository.SaveCallCount);
            Assert.Equal(initialStatus, order.Status);
            Assert.Null(order.ApprovedByUserId);
            Assert.Null(order.ApprovedAt);
            Assert.Empty(repository.SavedNotifications);
            Assert.Equal(0, notifier.CallCount);
        }
    }

    [Theory]
    [InlineData("ready", HttpStatusCode.NoContent)]
    [InlineData("officer", HttpStatusCode.Forbidden)]
    [InlineData("stale", HttpStatusCode.Conflict)]
    [InlineData("draft", HttpStatusCode.Conflict)]
    public async Task Reject_EnforcesPermissionVersionAndPendingStatus(
    string scenario,
    HttpStatusCode expectedStatus)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = scenario == "officer"
            ? [StockFlowRoles.PurchasingOfficer]
            : [StockFlowRoles.PurchasingManager];

        var order = new PurchaseOrder(
            "PO-REJECT-API",
            Guid.NewGuid(), "SUP-001", "Supplier",
            factory.Warehouse.Id, factory.Warehouse.Code, factory.Warehouse.Name,
            factory.OccurredAt.AddMinutes(-2), factory.User.Id);

        order.AddLine(
            factory.Product.Id, factory.Product.Sku, factory.Product.Name, 1, 10m);

        if (scenario != "draft")
        {
            order.Submit(factory.OccurredAt.AddMinutes(-1));
        }

        var initialStatus = order.Status;
        var repository = new RecordingLifecycleRepository(order);
        var notifier = new RecordingPurchasingNotifier(repository);
        using var host = CreateHost(factory, repository, notifier: notifier);
        using var client = await host.CreateClientWithAntiforgeryAsync();

        var version = scenario == "stale" ? Guid.NewGuid() : repository.Version;
        using var response = await client.PostAsJsonAsync(
            $"/api/purchase-orders/{order.Id}/reject",
            new { expectedVersion = version });

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(scenario == "officer" ? 0 : 1, repository.LookupCallCount);
        Assert.Null(order.ApprovedByUserId);
        Assert.Null(order.ApprovedAt);

        if (expectedStatus == HttpStatusCode.NoContent)
        {
            Assert.Equal(1, repository.SaveCallCount);
            Assert.Equal(PurchaseOrderStatus.Rejected, order.Status);
            Assert.Equal(factory.User.Id, order.RejectedByUserId);
            Assert.Equal(factory.OccurredAt, order.RejectedAt);
            Assert.Equal(1, notifier.CallCount);
            Assert.Equal(1, notifier.SaveCountAtNotification);
            Assert.Equal(order.CreatedByUserId, Assert.Single(notifier.RecipientIds));
            var notification = Assert.Single(repository.SavedNotifications);
            Assert.Equal(order.CreatedByUserId, notification.RecipientUserId);
            Assert.Equal(order.Id, notification.PurchaseOrderId);
            Assert.Equal(PurchasingNotificationKind.PurchaseOrderRejected, notification.Kind);
            Assert.Equal(factory.OccurredAt, notification.CreatedAt);
            Assert.Null(notification.ReadAt);
        }
        else
        {
            Assert.Equal(0, repository.SaveCallCount);
            Assert.Equal(initialStatus, order.Status);
            Assert.Null(order.RejectedByUserId);
            Assert.Null(order.RejectedAt);
            Assert.Empty(repository.SavedNotifications);
            Assert.Equal(0, notifier.CallCount);
        }
    }

    private static WebApplicationFactory<Program> CreateHost(
        StockFlowApiFactory factory,
        RecordingLifecycleRepository repository,
        FixedApproverRecipients? recipients = null,
        RecordingPurchasingNotifier? notifier = null)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPurchaseOrderRepository>();
                services.AddSingleton<IPurchaseOrderRepository>(repository);
                services.RemoveAll<IPurchasingNotificationRecipients>();
                services.AddSingleton<IPurchasingNotificationRecipients>(
                    recipients ?? new FixedApproverRecipients());
                services.RemoveAll<IPurchasingChangeNotifier>();
                services.AddSingleton<IPurchasingChangeNotifier>(
                    notifier ?? new RecordingPurchasingNotifier(repository));
            });
        });
    }

    private sealed class RecordingPurchasingNotifier(
        RecordingLifecycleRepository repository) : IPurchasingChangeNotifier
    {
        public int CallCount { get; private set; }
        public int SaveCountAtNotification { get; private set; }
        public IReadOnlyList<Guid> RecipientIds { get; private set; } = [];

        public Task NotifyAsync(
            IReadOnlyCollection<Guid> inboxRecipientIds,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            SaveCountAtNotification = repository.SaveCallCount;
            RecipientIds = inboxRecipientIds.ToArray();
            return Task.CompletedTask;
        }
    }

    private sealed class FixedApproverRecipients(params Guid[] userIds)
        : IPurchasingNotificationRecipients
    {
        public int LookupCallCount { get; private set; }

        public Task<IReadOnlyList<Guid>> GetApproverUserIdsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LookupCallCount++;
            return Task.FromResult<IReadOnlyList<Guid>>(userIds);
        }
    }

    private sealed class RecordingLifecycleRepository(PurchaseOrder order)
        : IPurchaseOrderRepository
    {
        public Guid Version { get; } = Guid.NewGuid();
        public int LookupCallCount { get; private set; }
        public int SaveCallCount { get; private set; }
        public IReadOnlyList<PurchasingNotification> SavedNotifications { get; private set; } = [];

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
            CancellationToken cancellationToken = default,
            IReadOnlyList<PurchasingNotification>? notifications = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Same(order, purchaseOrder);
            SaveCallCount++;
            SavedNotifications = notifications?.ToArray() ?? [];
            return Task.CompletedTask;
        }
    }
}
