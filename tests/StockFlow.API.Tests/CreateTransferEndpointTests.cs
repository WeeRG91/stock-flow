using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Application.Transfers.CreateTransfer;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

namespace StockFlow.API.Tests;

public sealed class CreateTransferEndpointTests
{
    [Theory]
    [InlineData("valid", HttpStatusCode.Created)]
    [InlineData("empty-draft", HttpStatusCode.Created)]
    [InlineData("same-warehouse", HttpStatusCode.BadRequest)]
    [InlineData("invalid-quantity", HttpStatusCode.BadRequest)]
    [InlineData("missing-product", HttpStatusCode.NotFound)]
    [InlineData("inactive-destination", HttpStatusCode.Conflict)]
    [InlineData("viewer", HttpStatusCode.Forbidden)]
    public async Task Create_EnforcesValidationAndPermission(
        string scenario,
        HttpStatusCode expectedStatus)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = scenario == "viewer"
            ? [StockFlowRoles.Viewer]
            : [StockFlowRoles.WarehouseStaff];

        var source = factory.Warehouse;
        var destination = new Warehouse("LIE", "Liège", "Belgium");
        if (scenario == "inactive-destination")
        {
            destination.Deactivate();
        }

        var transfers = new RecordingTransferRepository();
        var warehouses = new TransferWarehouses(source, destination);

        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITransferRepository>();
                services.RemoveAll<IWarehouseRepository>();
                services.AddSingleton<ITransferRepository>(transfers);
                services.AddSingleton<IWarehouseRepository>(warehouses);
            });
        });

        using var client = await host.CreateClientWithAntiforgeryAsync();

        var lines = scenario == "empty-draft"
            ? Array.Empty<CreateTransferLineCommand>()
            : new[]
            {
                new CreateTransferLineCommand(
                    scenario == "missing-product" ? Guid.NewGuid() : factory.Product.Id,
                    scenario is "invalid-quantity" or "viewer" ? 0 : 4)
            };

        using var response = await client.PostAsJsonAsync("/api/transfers", new
        {
            sourceWarehouseId = source.Id,
            destinationWarehouseId = scenario == "same-warehouse"
                ? source.Id
                : destination.Id,
            lines
        });

        Assert.Equal(expectedStatus, response.StatusCode);

        if (expectedStatus != HttpStatusCode.Created)
        {
            Assert.Equal(0, transfers.SaveCount);
            Assert.Null(transfers.SavedTransfer);
            return;
        }

        var result = await response.Content.ReadFromJsonAsync<CreateTransferResult>();
        Assert.NotNull(result);

        var saved = Assert.IsType<Transfer>(transfers.SavedTransfer);
        Assert.Equal(1, transfers.SaveCount);
        Assert.Equal(saved.Id, result.TransferId);
        Assert.Equal(TransferStatus.Draft, result.Status);
        Assert.Equal(factory.User.Id, result.CreatedByUserId);
        Assert.Equal(factory.OccurredAt, result.CreatedAt);
        Assert.Equal(source.Id, saved.SourceWarehouseId);
        Assert.Equal(destination.Id, saved.DestinationWarehouseId);

        if (scenario == "empty-draft")
        {
            Assert.Empty(saved.Lines);
        }
        else
        {
            var line = Assert.Single(saved.Lines);
            Assert.Equal(factory.Product.Id, line.ProductId);
            Assert.Equal(factory.Product.Sku, line.ProductSku);
            Assert.Equal(4, line.Quantity);
            Assert.Equal(0, line.InTransitQuantity);
        }
    }

    [Theory]
    [InlineData("ready", HttpStatusCode.NoContent)]
    [InlineData("stale", HttpStatusCode.Conflict)]
    [InlineData("dispatched", HttpStatusCode.Conflict)]
    [InlineData("viewer", HttpStatusCode.Forbidden)]
    [InlineData("missing-version", HttpStatusCode.BadRequest)]
    [InlineData("null-line", HttpStatusCode.BadRequest)]
    public async Task UpdateDraft_EnforcesVersionStatusAndPermission(
        string scenario, HttpStatusCode expectedStatus)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = scenario == "viewer"
            ? [StockFlowRoles.Viewer]
            : [StockFlowRoles.WarehouseStaff];

        var transfer = new Transfer(
            factory.Warehouse.Id, factory.Warehouse.Code, factory.Warehouse.Name,
            Guid.NewGuid(), "LIE", "Liège",
            factory.OccurredAt.AddMinutes(-1), factory.User.Id);
        transfer.AddLine(
            factory.Product.Id, factory.Product.Sku, factory.Product.Name, 4);
        var original = Assert.Single(transfer.Lines);

        if (scenario == "dispatched")
        {
            transfer.Dispatch(factory.OccurredAt, factory.User.Id);
        }

        var originalStatus = transfer.Status;
        var version = Guid.NewGuid();
        var repository = new RecordingTransferRepository { Loaded = (transfer, version) };
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITransferRepository>();
                services.AddSingleton<ITransferRepository>(repository);
            });
        });
        using var client = await host.CreateClientWithAntiforgeryAsync();

        var body = new Dictionary<string, object?>
        {
            ["lines"] = scenario == "null-line"
                ? new object?[] { null }
                : new object[]
                {
                    new
                    {
                        productId = factory.Product.Id,
                        quantity = scenario == "viewer" ? 0 : 8
                    }
                }
        };

        if (scenario != "missing-version")
        {
            body["expectedVersion"] = scenario == "stale" ? Guid.NewGuid() : version;
        }

        using var response = await client.PutAsJsonAsync(
            $"/api/transfers/{transfer.Id}", body);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Same(original, Assert.Single(transfer.Lines));
        Assert.Equal(originalStatus, transfer.Status);

        if (expectedStatus == HttpStatusCode.NoContent)
        {
            Assert.Equal(8, original.Quantity);
            Assert.Equal(1, repository.SaveCount);
            Assert.Same(transfer, repository.SavedTransfer);
        }
        else
        {
            Assert.Equal(4, original.Quantity);
            Assert.Equal(0, repository.SaveCount);
            Assert.Null(repository.SavedTransfer);
        }

        if (scenario is "viewer" or "missing-version" or "null-line")
        {
            Assert.Equal(0, repository.LookupCount);
        }
        else
        {
            Assert.Equal(1, repository.LookupCount);
        }
    }

    [Theory]
    [InlineData("ready", HttpStatusCode.NoContent)]
    [InlineData("missing-version", HttpStatusCode.BadRequest)]
    [InlineData("empty-version", HttpStatusCode.BadRequest)]
    [InlineData("missing-transfer", HttpStatusCode.NotFound)]
    [InlineData("stale", HttpStatusCode.Conflict)]
    [InlineData("dispatched", HttpStatusCode.Conflict)]
    [InlineData("viewer", HttpStatusCode.Forbidden)]
    [InlineData("missing-csrf", HttpStatusCode.BadRequest)]
    public async Task CancelDraft_EnforcesVersionStatusPermissionAndAntiforgery(
        string scenario, HttpStatusCode expectedStatus)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = scenario == "viewer"
            ? [StockFlowRoles.Viewer]
            : [StockFlowRoles.WarehouseStaff];

        var transfer = new Transfer(
            factory.Warehouse.Id, factory.Warehouse.Code, factory.Warehouse.Name,
            Guid.NewGuid(), "LIE", "Liège",
            factory.OccurredAt.AddMinutes(-1), factory.User.Id);
        transfer.AddLine(
            factory.Product.Id, factory.Product.Sku, factory.Product.Name, 4);
        var originalLine = Assert.Single(transfer.Lines);
        if (scenario == "dispatched")
        {
            transfer.Dispatch(factory.OccurredAt, factory.User.Id);
        }

        var originalStatus = transfer.Status;
        var version = Guid.NewGuid();
        var repository = new RecordingTransferRepository
        {
            Loaded = scenario == "missing-transfer" ? null : (transfer, version)
        };
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITransferRepository>();
                services.AddSingleton<ITransferRepository>(repository);
            });
        });
        using var client = await host.CreateClientWithAntiforgeryAsync();

        if (scenario == "missing-csrf")
        {
            // Preserve the authenticated client and cookies; omit only the CSRF header.
            client.DefaultRequestHeaders.Clear();
        }

        var body = new Dictionary<string, object?>();
        if (scenario != "missing-version")
        {
            body["expectedVersion"] = scenario switch
            {
                "empty-version" => Guid.Empty,
                "stale" => Guid.NewGuid(),
                _ => version
            };
        }

        using var response = await client.PostAsJsonAsync(
            $"/api/transfers/{transfer.Id}/cancel", body);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Same(originalLine, Assert.Single(transfer.Lines));
        Assert.Equal(4, originalLine.Quantity);
        Assert.Empty(transfer.PendingReceipts);
        Assert.Equal(0, repository.SaveCount);

        if (expectedStatus == HttpStatusCode.NoContent)
        {
            Assert.Equal(TransferStatus.Cancelled, transfer.Status);
            Assert.Equal(factory.OccurredAt, transfer.CancelledAt);
            Assert.Equal(factory.User.Id, transfer.CancelledByUserId);
            Assert.Equal(1, repository.CancellationSaveCount);
            Assert.Same(transfer, repository.SavedTransfer);
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }
        else
        {
            Assert.Equal(originalStatus, transfer.Status);
            Assert.Null(transfer.CancelledAt);
            Assert.Null(transfer.CancelledByUserId);
            Assert.Equal(0, repository.CancellationSaveCount);
            Assert.Null(repository.SavedTransfer);
            var problem = await response.Content
                .ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
            Assert.NotNull(problem);
            Assert.Equal((int)expectedStatus, problem.Status);
        }

        Assert.Equal(
            scenario is "viewer" or "missing-version" or "empty-version" or "missing-csrf"
                ? 0 : 1,
            repository.LookupCount);
    }

    [Theory]
    [InlineData("ready", HttpStatusCode.NoContent)]
    [InlineData("missing-version", HttpStatusCode.BadRequest)]
    [InlineData("empty-version", HttpStatusCode.BadRequest)]
    [InlineData("missing-transfer", HttpStatusCode.NotFound)]
    [InlineData("stale", HttpStatusCode.Conflict)]
    [InlineData("dispatched", HttpStatusCode.Conflict)]
    [InlineData("insufficient", HttpStatusCode.Conflict)]
    [InlineData("viewer", HttpStatusCode.Forbidden)]
    [InlineData("missing-csrf", HttpStatusCode.BadRequest)]
    [InlineData("save-conflict", HttpStatusCode.Conflict)]
    [InlineData("unexpected", HttpStatusCode.InternalServerError)]
    public async Task Dispatch_EnforcesValidationPermissionAndPersistenceErrors(
        string scenario, HttpStatusCode expectedStatus)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.Roles = scenario == "viewer"
            ? [StockFlowRoles.Viewer] : [StockFlowRoles.WarehouseStaff];
        var destination = new Warehouse("LIE", "Liège", "Belgium");
        var transfer = new Transfer(
            factory.Warehouse.Id, factory.Warehouse.Code, factory.Warehouse.Name,
            destination.Id, destination.Code, destination.Name,
            factory.OccurredAt.AddMinutes(-1), factory.User.Id);
        transfer.AddLine(factory.Product.Id, factory.Product.Sku, factory.Product.Name, 4);
        if (scenario == "dispatched")
            transfer.Dispatch(factory.OccurredAt, factory.User.Id);
        var originalStatus = transfer.Status;

        var inventory = new StockFlow.Domain.Entities.Inventory(
            factory.Product.Id, factory.Warehouse.Id);
        var startingQuantity = scenario == "insufficient" ? 3 : 10;
        inventory.ReceiveStock(startingQuantity, "Opening stock", factory.OccurredAt);
        inventory.AcknowledgePersistedMovements(
            inventory.PendingMovements.Select(movement => movement.Id).ToArray());
        factory.Inventories.Inventory = inventory;

        var version = Guid.NewGuid();
        var repository = new RecordingTransferRepository
        {
            Loaded = scenario == "missing-transfer" ? null : (transfer, version),
            DispatchFailure = scenario switch
            {
                "save-conflict" => new TransferConflictException(transfer.Id, "Concurrent update."),
                "unexpected" => new InvalidOperationException("Sensitive dispatch failure detail."),
                _ => null
            }
        };
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITransferRepository>();
                services.RemoveAll<IWarehouseRepository>();
                services.AddSingleton<ITransferRepository>(repository);
                services.AddSingleton<IWarehouseRepository>(
                    new TransferWarehouses(factory.Warehouse, destination));
            });
        });
        using var client = await host.CreateClientWithAntiforgeryAsync();
        if (scenario == "missing-csrf") client.DefaultRequestHeaders.Clear();

        var body = new Dictionary<string, object?>();
        if (scenario != "missing-version")
            body["expectedVersion"] = scenario switch
            {
                "empty-version" => Guid.Empty,
                "stale" => Guid.NewGuid(),
                _ => version
            };

        using var response = await client.PostAsJsonAsync(
            $"/api/transfers/{transfer.Id}/dispatch", body);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(0, repository.SaveCount);
        Assert.Equal(0, repository.CancellationSaveCount);
        Assert.Equal(0, factory.Inventories.SaveCallCount);

        if (scenario is "ready" or "save-conflict" or "unexpected")
        {
            Assert.Equal(1, repository.DispatchSaveCount);
            Assert.Equal(TransferStatus.Dispatched, transfer.Status);
            Assert.Equal(factory.OccurredAt, transfer.DispatchedAt);
            Assert.Equal(factory.User.Id, transfer.DispatchedByUserId);
            Assert.Equal(6, inventory.QuantityOnHand);
            var movement = Assert.Single(inventory.PendingMovements);
            Assert.Equal(-4, movement.QuantityChange);
            Assert.Equal(StockMovementType.Removal, movement.Type);
            Assert.Equal(factory.OccurredAt, movement.OccurredAt);
            if (scenario == "ready")
            {
                Assert.Same(transfer, repository.SavedTransfer);
                Assert.Same(inventory, Assert.Single(repository.DispatchInventories));
                Assert.Empty(await response.Content.ReadAsStringAsync());
            }
            else
                Assert.Null(repository.SavedTransfer);
        }
        else
        {
            Assert.Equal(0, repository.DispatchSaveCount);
            Assert.Equal(originalStatus, transfer.Status);
            Assert.Equal(startingQuantity, inventory.QuantityOnHand);
            Assert.Empty(inventory.PendingMovements);
        }

        if (expectedStatus != HttpStatusCode.NoContent)
        {
            var problem = await response.Content
                .ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
            Assert.NotNull(problem);
            Assert.Equal((int)expectedStatus, problem.Status);
            Assert.DoesNotContain("Sensitive dispatch failure detail.",
                await response.Content.ReadAsStringAsync());
        }
    }

    private sealed class RecordingTransferRepository : ITransferRepository
    {
        public int SaveCount { get; private set; }
        public int CancellationSaveCount { get; private set; }
        public int DispatchSaveCount { get; private set; }
        public Exception? DispatchFailure { get; init; }
        public IReadOnlyList<StockFlow.Domain.Entities.Inventory> DispatchInventories
            { get; private set; } = [];
        public Transfer? SavedTransfer { get; private set; }
        public (Transfer Transfer, Guid Version)? Loaded { get; set; }
        public int LookupCount { get; private set; }

        public Task<(Transfer Transfer, Guid Version)?> GetByIdAsync(
            Guid transferId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LookupCount++;

            var loaded = Loaded;
            var result = loaded is { } entry && entry.Transfer.Id == transferId
                ? loaded
                : null;

            return Task.FromResult(result);
        }

        public Task SaveCancellationAsync(
            Transfer transfer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CancellationSaveCount++;
            SavedTransfer = transfer;
            return Task.CompletedTask;
        }

        public Task SaveDispatchAsync(
            Transfer transfer,
            IReadOnlyList<StockFlow.Domain.Entities.Inventory> sourceInventories,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DispatchSaveCount++;
            if (DispatchFailure is { } failure)
                return Task.FromException(failure);
            SavedTransfer = transfer;
            DispatchInventories = sourceInventories.ToArray();
            return Task.CompletedTask;
        }

        public Task SaveDraftAsync(
            Transfer transfer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCount++;
            SavedTransfer = transfer;
            return Task.CompletedTask;
        }
    }

    private sealed class TransferWarehouses(params Warehouse[] warehouses)
        : IWarehouseRepository
    {
        public Task<Warehouse?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(warehouses.SingleOrDefault(warehouse => warehouse.Id == id));
        }
    }
}
