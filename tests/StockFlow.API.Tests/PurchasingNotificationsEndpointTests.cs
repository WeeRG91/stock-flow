using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Authorization;
using StockFlow.Application.PurchasingNotifications;
using StockFlow.Application.PurchasingNotifications.GetPurchasingNotifications;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.API.Tests;

public sealed class PurchasingNotificationsEndpointTests
{
    private const string Endpoint = "/api/purchasing-notifications";

    [Theory]
    [InlineData("viewer", HttpStatusCode.OK)]
    [InlineData("invalid-page", HttpStatusCode.BadRequest)]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("forbidden", HttpStatusCode.Forbidden)]
    public async Task GetInbox_UsesSessionUserAndValidatesAccess(
        string scenario, HttpStatusCode expectedStatus)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.IsAuthenticated = scenario != "anonymous";
        factory.User.Roles = scenario == "forbidden"
            ? ["UnknownRole"]
            : [StockFlowRoles.Viewer];

        var store = new InboxStore(CreateNotification(factory, factory.User.Id));
        using var host = CreateHost(factory, store);
        using var client = host.CreateClient();

        var page = scenario == "invalid-page" ? 0 : 2;
        using var response = await client.GetAsync(
            $"{Endpoint}?pageNumber={page}&pageSize=10&recipientUserId={Guid.NewGuid()}");

        Assert.Equal(expectedStatus, response.StatusCode);

        if (expectedStatus != HttpStatusCode.OK)
        {
            Assert.Null(store.RequestedPage);
            return;
        }

        Assert.Equal((factory.User.Id, 2, 10), store.RequestedPage.GetValueOrDefault());
        var inbox = await response.Content.ReadFromJsonAsync<GetPurchasingNotificationsResult>();
        Assert.NotNull(inbox);
        Assert.Equal(2, inbox.PageNumber);
        Assert.Equal(10, inbox.PageSize);
        Assert.Equal(1, inbox.UnreadCount);
        Assert.Empty(inbox.Items);
    }

    [Theory]
    [InlineData("owned", HttpStatusCode.NoContent)]
    [InlineData("other-user", HttpStatusCode.NotFound)]
    [InlineData("missing", HttpStatusCode.NotFound)]
    public async Task MarkAsRead_RequiresOwnershipAndPreservesFirstReadTime(
        string scenario, HttpStatusCode expectedStatus)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = [StockFlowRoles.Viewer];

        var recipientId = scenario == "other-user" ? Guid.NewGuid() : factory.User.Id;
        var notification = CreateNotification(factory, recipientId);
        var store = new InboxStore(notification);
        using var host = CreateHost(factory, store);
        using var client = await host.CreateClientWithAntiforgeryAsync();

        var id = scenario == "missing" ? Guid.NewGuid() : notification.Id;
        var path = $"{Endpoint}/{id}/read";

        using var response = await client.PutAsync(path, content: null);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal((id, factory.User.Id), store.RequestedItem.GetValueOrDefault());

        if (expectedStatus != HttpStatusCode.NoContent)
        {
            Assert.Equal(0, store.SaveCount);
            Assert.Null(notification.ReadAt);
            return;
        }

        Assert.Equal(1, store.SaveCount);
        Assert.Equal(factory.OccurredAt, notification.ReadAt);

        using var replay = await client.PutAsync(path, content: null);

        Assert.Equal(HttpStatusCode.NoContent, replay.StatusCode);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(factory.OccurredAt, notification.ReadAt);
    }

    private static PurchasingNotification CreateNotification(
        StockFlowApiFactory factory, Guid recipientId)
    {
        return new PurchasingNotification(
            recipientId, Guid.NewGuid(),
            PurchasingNotificationKind.PurchaseOrderApproved,
            factory.OccurredAt.AddMinutes(-1));
    }

    private static WebApplicationFactory<Program> CreateHost(
        StockFlowApiFactory factory, InboxStore store)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPurchasingNotificationReadService>();
                services.RemoveAll<IPurchasingNotificationRepository>();
                services.AddSingleton<IPurchasingNotificationReadService>(store);
                services.AddSingleton<IPurchasingNotificationRepository>(store);
            });
        });
    }

    private sealed class InboxStore(PurchasingNotification notification)
        : IPurchasingNotificationReadService, IPurchasingNotificationRepository
    {
        public (Guid RecipientId, int Page, int Size)? RequestedPage { get; private set; }
        public (Guid NotificationId, Guid RecipientId)? RequestedItem { get; private set; }
        public int SaveCount { get; private set; }

        public Task<GetPurchasingNotificationsResult> GetPageAsync(
            Guid recipientUserId, int pageNumber, int pageSize,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestedPage = (recipientUserId, pageNumber, pageSize);
            return Task.FromResult(
                new GetPurchasingNotificationsResult([], pageNumber, pageSize, 1, 1));
        }

        public Task<PurchasingNotification?> GetForRecipientAsync(
            Guid notificationId, Guid recipientUserId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestedItem = (notificationId, recipientUserId);

            return Task.FromResult<PurchasingNotification?>(
                notification.Id == notificationId &&
                notification.RecipientUserId == recipientUserId
                    ? notification
                    : null);
        }

        public Task SaveAsync(
            PurchasingNotification item,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Same(notification, item);
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}