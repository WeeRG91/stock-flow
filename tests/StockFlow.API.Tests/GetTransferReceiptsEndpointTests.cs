using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockFlow.Application.Authorization;
using StockFlow.Application.Transfers;
using StockFlow.Application.Transfers.GetTransferReceipts;
using StockFlow.Application.Transfers.GetTransfers;
using StockFlow.Domain.Enums;

namespace StockFlow.API.Tests;

public sealed class GetTransferReceiptsEndpointTests
{
    [Theory]
    [InlineData("viewer", HttpStatusCode.OK)]
    [InlineData("empty", HttpStatusCode.OK)]
    [InlineData("missing", HttpStatusCode.NotFound)]
    [InlineData("empty-id", HttpStatusCode.BadRequest)]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("unknown-role", HttpStatusCode.Forbidden)]
    [InlineData("failure", HttpStatusCode.InternalServerError)]
    public async Task GetReceipts_EnforcesReadAccessAndReturnsHistoryOrProblem(
        string scenario, HttpStatusCode expectedStatus)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.IsAuthenticated = scenario != "anonymous";
        factory.User.Roles = scenario == "unknown-role" ? ["UnknownRole"] : [StockFlowRoles.Viewer];
        var transferId = scenario == "empty-id" ? Guid.Empty : Guid.NewGuid();
        var receipt = new TransferReceiptHistoryItemDto(Guid.NewGuid(), factory.OccurredAt,
            Guid.NewGuid(), [new(Guid.NewGuid(), 1), new(Guid.NewGuid(), 2)]);
        var reader = new RecordingReader
        {
            History = scenario switch
            {
                "missing" => null,
                "empty" => [],
                _ => [receipt]
            },
            Fail = scenario == "failure"
        };
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITransferReadService>();
            services.AddSingleton<ITransferReadService>(reader);
        }));
        // No antiforgery token: a read-only GET must not require one.
        using var client = host.CreateClient();

        using var response = await client.GetAsync($"/api/transfers/{transferId}/receipts");

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(scenario is "empty-id" or "anonymous" or "unknown-role" ? 0 : 1,
            reader.CallCount);
        if (expectedStatus == HttpStatusCode.OK)
        {
            Assert.Equal(transferId, reader.RequestedId);
            var history = await response.Content.ReadFromJsonAsync<TransferReceiptHistoryItemDto[]>();
            Assert.NotNull(history);
            if (scenario == "empty")
            {
                Assert.Empty(history);
            }
            else
            {
                var item = Assert.Single(history);
                Assert.Equal(receipt.ReceiptId, item.ReceiptId);
                Assert.Equal(receipt.ConfirmedAt, item.ConfirmedAt);
                Assert.Equal(receipt.ConfirmedByUserId, item.ConfirmedByUserId);
                Assert.Equal(receipt.Lines.ToArray(), item.Lines.ToArray());
            }
        }
        else
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.NotNull(problem);
            Assert.Equal((int)expectedStatus, problem.Status);
            Assert.DoesNotContain("Sensitive receipt history failure.",
                await response.Content.ReadAsStringAsync());
        }
    }

    private sealed class RecordingReader : ITransferReadService
    {
        public IReadOnlyList<TransferReceiptHistoryItemDto>? History { get; init; }
        public bool Fail { get; init; }
        public int CallCount { get; private set; }
        public Guid RequestedId { get; private set; }

        public Task<IReadOnlyList<TransferReceiptHistoryItemDto>?> GetReceiptsAsync(
            Guid transferId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            RequestedId = transferId;
            if (Fail) throw new InvalidOperationException("Sensitive receipt history failure.");
            return Task.FromResult(History);
        }

        public Task<TransferDetailsDto?> GetByIdAsync(Guid transferId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("History requests must not load details.");

        public Task<GetTransfersResult> GetPageAsync(int pageNumber, int pageSize,
            TransferStatus? status, string? search, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("History requests must not load the transfer list.");
    }
}
