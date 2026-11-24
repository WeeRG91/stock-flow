using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockFlow.Application.Authorization;
using StockFlow.Application.Transfers;
using StockFlow.Application.Transfers.GetTransfers;
using StockFlow.Domain.Enums;

namespace StockFlow.API.Tests;

public sealed class GetTransferByIdEndpointTests
{
    [Theory]
    [InlineData("viewer", HttpStatusCode.OK)]
    [InlineData("empty-id", HttpStatusCode.BadRequest)]
    [InlineData("missing", HttpStatusCode.NotFound)]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("unknown-role", HttpStatusCode.Forbidden)]
    [InlineData("failure", HttpStatusCode.InternalServerError)]
    public async Task GetById_EnforcesReadAccessAndReturnsDetailsOrProblem(
        string scenario, HttpStatusCode expectedStatus)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.IsAuthenticated = scenario != "anonymous";
        factory.User.Roles = scenario == "unknown-role" ? ["UnknownRole"] : [StockFlowRoles.Viewer];
        var id = Guid.NewGuid();
        var dispatchedAt = factory.OccurredAt.AddMinutes(1);
        var line = new TransferLineDto(Guid.NewGuid(), factory.Product.Id,
            "HISTORICAL-SKU", "Historical product name", 5, 5, 2);
        var details = new TransferDetailsDto(id, factory.Warehouse.Id, "BRU", "Brussels snapshot",
            Guid.NewGuid(), "LIE", "Liège snapshot", TransferStatus.PartiallyReceived,
            factory.OccurredAt, factory.User.Id, dispatchedAt, factory.User.Id,
            null, null, null, Guid.NewGuid(), [line]);
        var reader = new RecordingReader
        {
            Details = scenario == "missing" ? null : details,
            Fail = scenario == "failure"
        };
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITransferReadService>();
            services.AddSingleton<ITransferReadService>(reader);
        }));
        using var client = host.CreateClient();
        var requestedId = scenario == "empty-id" ? Guid.Empty : id;

        using var response = await client.GetAsync($"/api/transfers/{requestedId}");

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(scenario is "empty-id" or "anonymous" or "unknown-role" ? 0 : 1,
            reader.CallCount);
        if (expectedStatus == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<TransferDetailsDto>();
            Assert.NotNull(result);
            Assert.Equal(id, reader.RequestedId);
            Assert.Equal(details.Id, result.Id);
            Assert.Equal(details.Version, result.Version);
            Assert.Equal(details.SourceWarehouseId, result.SourceWarehouseId);
            Assert.Equal(details.DestinationWarehouseId, result.DestinationWarehouseId);
            Assert.Equal(details.SourceWarehouseName, result.SourceWarehouseName);
            Assert.Equal(details.DestinationWarehouseName, result.DestinationWarehouseName);
            Assert.Equal(details.Status, result.Status);
            Assert.Equal(details.CreatedAt, result.CreatedAt);
            Assert.Equal(details.CreatedByUserId, result.CreatedByUserId);
            Assert.Equal(dispatchedAt, result.DispatchedAt);
            Assert.Equal(details.DispatchedByUserId, result.DispatchedByUserId);
            Assert.Null(result.CancelledAt);
            Assert.Null(result.CancelledByUserId);
            Assert.Null(result.ReceivedAt);
            Assert.Equal(line, Assert.Single(result.Lines));
            Assert.Equal(3, result.Lines[0].InTransitQuantity);
            using var json = System.Text.Json.JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());
            Assert.Equal(3, json.RootElement.GetProperty("lines")[0]
                .GetProperty("inTransitQuantity").GetInt32());
        }
        else
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.NotNull(problem);
            Assert.Equal((int)expectedStatus, problem.Status);
            Assert.DoesNotContain("Sensitive transfer detail failure.",
                await response.Content.ReadAsStringAsync());
        }
    }

    private sealed class RecordingReader : ITransferReadService
    {
        public TransferDetailsDto? Details { get; init; }
        public bool Fail { get; init; }
        public int CallCount { get; private set; }
        public Guid RequestedId { get; private set; }

        public Task<TransferDetailsDto?> GetByIdAsync(Guid transferId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            RequestedId = transferId;
            if (Fail) throw new InvalidOperationException("Sensitive transfer detail failure.");
            return Task.FromResult(Details);
        }

        public Task<GetTransfersResult> GetPageAsync(int pageNumber, int pageSize,
            TransferStatus? status, string? search, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Detail tests must not request a list.");
    }
}
