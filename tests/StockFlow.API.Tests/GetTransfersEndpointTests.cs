using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockFlow.Application.Authorization;
using StockFlow.Application.Transfers;
using StockFlow.Application.Transfers.GetTransfers;
using StockFlow.Application.Transfers.GetTransferReceipts;
using StockFlow.Domain.Enums;

namespace StockFlow.API.Tests;

public sealed class GetTransfersEndpointTests
{
    [Theory]
    [InlineData("", HttpStatusCode.OK)]
    [InlineData("?pageNumber=2&pageSize=10&status=Dispatched&search=%20BRU%20", HttpStatusCode.OK)]
    [InlineData("?pageNumber=0", HttpStatusCode.BadRequest)]
    [InlineData("?pageSize=0", HttpStatusCode.BadRequest)]
    [InlineData("?pageSize=101", HttpStatusCode.BadRequest)]
    [InlineData("?pageNumber=2147483647&pageSize=100", HttpStatusCode.BadRequest)]
    [InlineData("?status=999", HttpStatusCode.BadRequest)]
    [InlineData("?status=invalid", HttpStatusCode.BadRequest)]
    [InlineData("?pageNumber=invalid", HttpStatusCode.BadRequest)]
    public async Task GetPage_ViewerCanReadAndInvalidQueriesAreRejected(
        string queryString, HttpStatusCode expectedStatus)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = [StockFlowRoles.Viewer];
        var reader = new RecordingReader();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITransferReadService>();
            services.AddSingleton<ITransferReadService>(reader);
        }));
        // GET must work without an antiforgery token.
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/api/transfers" + queryString);

        Assert.Equal(expectedStatus, response.StatusCode);
        if (expectedStatus == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<GetTransfersResult>();
            Assert.NotNull(result);
            Assert.Equal(1, reader.CallCount);
            Assert.Equal(queryString == "" ? new GetTransfersQuery()
                : new GetTransfersQuery(2, 10, TransferStatus.Dispatched, "BRU"), reader.Query);
            Assert.Equal(reader.Query!.PageNumber, result.PageNumber);
            Assert.Equal(reader.Query.PageSize, result.PageSize);
            Assert.Equal(1, result.TotalCount);
            Assert.Equal(reader.Item, Assert.Single(result.Items));
        }
        else
        {
            Assert.Equal(0, reader.CallCount);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.NotNull(problem);
            Assert.Equal(400, problem.Status);
        }
    }

    [Theory]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("unknown-role", HttpStatusCode.Forbidden)]
    [InlineData("empty", HttpStatusCode.OK)]
    [InlineData("failure", HttpStatusCode.InternalServerError)]
    public async Task GetPage_HandlesAccessEmptyResultsAndUnexpectedFailure(
        string scenario, HttpStatusCode expectedStatus)
    {
        using var factory = new StockFlowApiFactory();
        if (scenario == "anonymous") factory.User.IsAuthenticated = false;
        factory.User.Roles = scenario == "unknown-role" ? ["UnknownRole"] : [StockFlowRoles.Viewer];
        var reader = new RecordingReader { Empty = scenario == "empty", Fail = scenario == "failure" };
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITransferReadService>();
            services.AddSingleton<ITransferReadService>(reader);
        }));
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/api/transfers");

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(scenario is "empty" or "failure" ? 1 : 0, reader.CallCount);
        if (scenario == "empty")
        {
            var result = await response.Content.ReadFromJsonAsync<GetTransfersResult>();
            Assert.NotNull(result);
            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalCount);
            Assert.Equal(1, result.PageNumber);
            Assert.Equal(20, result.PageSize);
        }
        else
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.NotNull(problem);
            Assert.Equal((int)expectedStatus, problem.Status);
            Assert.DoesNotContain("Sensitive transfer query failure.",
                await response.Content.ReadAsStringAsync());
        }
    }

    private sealed class RecordingReader : ITransferReadService
    {
        public Task<IReadOnlyList<TransferReceiptHistoryItemDto>?> GetReceiptsAsync(
            Guid transferId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "These tests must not request transfer receipt history.");
        }

        public Task<TransferDetailsDto?> GetByIdAsync(
            Guid transferId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "These list tests must not request transfer details.");
        }

        public int CallCount { get; private set; }
        public GetTransfersQuery? Query { get; private set; }
        public bool Empty { get; init; }
        public bool Fail { get; init; }
        public TransferSummaryDto Item { get; } = new(Guid.NewGuid(), "BRU", "Brussels",
            "LIE", "Liège", TransferStatus.Dispatched,
            new DateTimeOffset(2026, 11, 23, 11, 0, 0, TimeSpan.Zero), 2)
        {
            DispatchedAt = new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero)
        };

        public Task<GetTransfersResult> GetPageAsync(int pageNumber, int pageSize,
            TransferStatus? status, string? search, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            Query = new(pageNumber, pageSize, status, search);
            if (Fail) throw new InvalidOperationException("Sensitive transfer query failure.");
            return Task.FromResult(new GetTransfersResult(Empty ? [] : [Item],
                pageNumber, pageSize, Empty ? 0 : 1));
        }
    }
}
