using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockFlow.Application.Authorization;
using StockFlow.Application.PurchaseOrders;
using StockFlow.Application.PurchaseOrders.GetPurchaseOrders;

namespace StockFlow.API.Tests;

public class GetPurchaseOrdersEndpointTests
{
    private const string Endpoint = "/api/purchase-orders";

    [Theory]
    [InlineData("", 1, 20)]
    [InlineData("?pageNumber=2&pageSize=10", 2, 10)]
    public async Task GetPage_AsViewer_UsesRequestedPaging(
        string queryString,
        int pageNumber,
        int pageSize)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = [StockFlowRoles.Viewer];

        var reader = new RecordingOrderPageReader();
        using var host = CreateHost(factory, reader);
        using var client = host.CreateClient();

        using var response = await client.GetAsync(Endpoint + queryString);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<GetPurchaseOrdersResult>();

        Assert.NotNull(result);
        Assert.Equal((pageNumber, pageSize), reader.RequestedPage.GetValueOrDefault());
        Assert.Equal(pageNumber, result.PageNumber);
        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(42, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Theory]
    [InlineData("?pageNumber=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?pageNumber=2147483647&pageSize=2")]
    [InlineData("?pageNumber=abc")]
    public async Task GetPage_WithInvalidPaging_SkipsReadService(string queryString)
    {
        using var factory = new StockFlowApiFactory();
        var reader = new RecordingOrderPageReader();
        using var host = CreateHost(factory, reader);
        using var client = host.CreateClient();

        using var response = await client.GetAsync(Endpoint + queryString);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(reader.RequestedPage);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task GetPage_WithoutReadAccess_SkipsReadService(
        bool authenticated,
        HttpStatusCode expectedStatus)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.IsAuthenticated = authenticated;
        factory.User.Roles = ["UnknownRole"];

        var reader = new RecordingOrderPageReader();
        using var host = CreateHost(factory, reader);
        using var client = host.CreateClient();

        using var response = await client.GetAsync(Endpoint);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Null(reader.RequestedPage);
    }

    private static WebApplicationFactory<Program> CreateHost(
        StockFlowApiFactory factory,
        RecordingOrderPageReader reader)
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

    private sealed class RecordingOrderPageReader : IPurchaseOrderReadService
    {
        public (int PageNumber, int PageSize)? RequestedPage { get; private set; }

        public Task<PurchaseOrderDetailsDto?> GetByIdAsync(
            Guid purchaseOrderId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<GetPurchaseOrdersResult> GetPageAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestedPage = (pageNumber, pageSize);

            return Task.FromResult(
                new GetPurchaseOrdersResult([], pageNumber, pageSize, 42));
        }
    }
}