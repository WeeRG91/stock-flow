using Microsoft.AspNetCore.Hosting;
using StockFlow.Application.Exceptions;
using StockFlow.Application.Inventory.ReceiveStock;
using StockFlow.Domain.Enums;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.API.Tests
{
    public class ReceiveStockEndpointTests
    {
        private const string Endpoint = "/api/inventory/receive";

        private static JsonObject ValidRequest(StockFlowApiFactory factory)
        {
            return new JsonObject
            {
                ["productId"] = factory.Product.Id.ToString(),
                ["warehouseId"] = factory.Warehouse.Id.ToString(),
                ["quantity"] = 10
            };
        }

        private static async Task<JsonElement> AssertProblem(
            HttpResponseMessage response,
            HttpStatusCode expectedStatus)
        {
            Assert.Equal(expectedStatus, response.StatusCode);
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);

            var problem = await response.Content
                .ReadFromJsonAsync<JsonElement>();

            Assert.Equal(
                (int)expectedStatus,
                problem.GetProperty("status").GetInt32());

            Assert.False(string.IsNullOrWhiteSpace(
                problem.GetProperty("title").GetString()));

            return problem;
        }

        [Fact]
        public async Task Receive_WithValidRequest_ReturnsBalance()
        {
            using var factory = new StockFlowApiFactory();
            using var client = factory.CreateClient();

            using var response = await client.PostAsJsonAsync(
                Endpoint,
                ValidRequest(factory));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content
                .ReadFromJsonAsync<ReceiveStockResult>();

            Assert.NotNull(result);

            var saved = factory.Inventories.LastSavedInventory;
            Assert.NotNull(saved);

            Assert.Equal(saved.Id, result.InventoryId);
            Assert.Equal(10, result.QuantityOnHand);
            Assert.Equal(factory.Product.Id, saved.ProductId);
            Assert.Equal(factory.Warehouse.Id, saved.WarehouseId);
            Assert.Equal(1, factory.Inventories.SaveCallCount);

            var movement = Assert.Single(saved.PendingMovements);
            Assert.Equal(10, movement.QuantityChange);
            Assert.Equal(StockMovementType.Receipt, movement.Type);
            Assert.Equal(factory.OccurredAt, movement.OccurredAt);
        }

        [Theory]
        [InlineData("productId", "missing")]
        [InlineData("productId", "null")]
        [InlineData("productId", "empty")]
        [InlineData("productId", "malformed")]
        [InlineData("warehouseId", "missing")]
        [InlineData("warehouseId", "null")]
        [InlineData("warehouseId", "empty")]
        [InlineData("warehouseId", "malformed")]
        [InlineData("quantity", "missing")]
        [InlineData("quantity", "null")]
        [InlineData("quantity", "zero")]
        [InlineData("quantity", "negative")]
        public async Task Receive_WithInvalidField_ReturnsValidationProblem(
            string field,
            string invalidValue)
        {
            using var factory = new StockFlowApiFactory();
            using var client = factory.CreateClient();

            var request = ValidRequest(factory);

            switch (invalidValue)
            {
                case "missing":
                    request.Remove(field);
                    break;
                case "null":
                    request[field] = null;
                    break;
                case "empty":
                    request[field] = Guid.Empty.ToString();
                    break;
                case "malformed":
                    request[field] = "not-a-guid";
                    break;
                case "zero":
                    request[field] = 0;
                    break;
                case "negative":
                    request[field] = -1;
                    break;
            }

            using var response = await client.PostAsJsonAsync(
                Endpoint,
                request);

            var problem = await AssertProblem(
                response,
                HttpStatusCode.BadRequest);

            Assert.True(
                problem.GetProperty("errors")
                    .EnumerateObject()
                    .Any());

            Assert.Equal(0, factory.Inventories.GetCallCount);
            Assert.Equal(0, factory.Inventories.SaveCallCount);
        }

        [Fact]
        public async Task Receive_WithMalformedJson_ReturnsBadRequest()
        {
            using var factory = new StockFlowApiFactory();
            using var client = factory.CreateClient();
            using var content = new StringContent(
                "{",
                Encoding.UTF8,
                "application/json");

            using var response = await client.PostAsync(Endpoint, content);

            await AssertProblem(response, HttpStatusCode.BadRequest);

            Assert.Equal(0, factory.Inventories.GetCallCount);
            Assert.Equal(0, factory.Inventories.SaveCallCount);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Receive_WithMissingResource_ReturnsNotFound(
            bool missingProduct)
        {
            using var factory = new StockFlowApiFactory();

            if (missingProduct)
            {
                factory.Products.Product = null;
            }
            else
            {
                factory.Warehouses.Warehouse = null;
            }

            using var client = factory.CreateClient();

            using var response = await client.PostAsJsonAsync(
                Endpoint,
                ValidRequest(factory));

            await AssertProblem(response, HttpStatusCode.NotFound);

            Assert.Equal(0, factory.Inventories.GetCallCount);
            Assert.Equal(0, factory.Inventories.SaveCallCount);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Receive_WithInactiveResource_ReturnsConflict(
            bool inactiveProduct)
        {
            using var factory = new StockFlowApiFactory();

            if (inactiveProduct)
            {
                factory.Product.Deactivate();
            }
            else
            {
                factory.Warehouse.Deactivate();
            }

            using var client = factory.CreateClient();

            using var response = await client.PostAsJsonAsync(
                Endpoint,
                ValidRequest(factory));

            var problem = await AssertProblem(
                response,
                HttpStatusCode.Conflict);

            Assert.Equal(
                "Business rule violation",
                problem.GetProperty("title").GetString());

            Assert.Equal(0, factory.Inventories.SaveCallCount);
        }

        [Fact]
        public async Task Receive_WhenInventoryConflicts_ReturnsConflict()
        {
            using var factory = new StockFlowApiFactory();

            factory.Inventories.SaveException =
                new InventoryConflictException(
                    factory.Product.Id,
                    factory.Warehouse.Id);

            using var client = factory.CreateClient();

            using var response = await client.PostAsJsonAsync(
                Endpoint,
                ValidRequest(factory));

            var problem = await AssertProblem(
                response,
                HttpStatusCode.Conflict);

            Assert.Equal(
                "Inventory conflict",
                problem.GetProperty("title").GetString());

            Assert.Equal(1, factory.Inventories.SaveCallCount);
        }

        [Fact]
        public async Task Receive_WhenUnexpectedFailureOccurs_ReturnsSafeServerError()
        {
            using var factory = new StockFlowApiFactory();

            const string internalMessage =
                "Sensitive internal database diagnostics.";

            factory.Inventories.SaveException =
                new InvalidOperationException(internalMessage);

            using var client = factory.CreateClient();

            using var response = await client.PostAsJsonAsync(
                Endpoint,
                ValidRequest(factory));

            var problem = await AssertProblem(
                response,
                HttpStatusCode.InternalServerError);

            Assert.Equal(
                "An unexpected error occurred.",
                problem.GetProperty("detail").GetString());

            Assert.True(problem.TryGetProperty("traceId", out _));

            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(internalMessage, body);
            Assert.DoesNotContain("InvalidOperationException", body);
        }

        [Fact]
        public async Task Receive_WhenStockWouldOverflow_ReturnsConflictWithoutSaving()
        {
            using var factory = new StockFlowApiFactory();

            var inventory = new InventoryEntity(
                factory.Product.Id,
                factory.Warehouse.Id);

            inventory.ReceiveStock(
                int.MaxValue,
                "Initial stock",
                factory.OccurredAt);

            var movementsBefore = inventory.PendingMovements.ToArray();
            factory.Inventories.Inventory = inventory;

            using var client = factory.CreateClient();

            var request = ValidRequest(factory);
            request["quantity"] = 1;

            using var response = await client.PostAsJsonAsync(Endpoint, request);

            var problem = await AssertProblem(
                response,
                HttpStatusCode.Conflict);

            Assert.Equal(
                "Business rule violation",
                problem.GetProperty("title").GetString());

            Assert.Equal(int.MaxValue, inventory.QuantityOnHand);
            Assert.Equal(movementsBefore, inventory.PendingMovements.ToArray());
            Assert.Equal(0, factory.Inventories.SaveCallCount);
        }

        [Theory]
        [InlineData("Development", HttpStatusCode.OK)]
        [InlineData("Testing", HttpStatusCode.NotFound)]
        public async Task OpenApi_IsAvailableOnlyInDevelopment(
            string environment,
            HttpStatusCode expectedStatus)
        {
            using var factory = new StockFlowApiFactory();

            using var configuredFactory = factory.WithWebHostBuilder(
                builder => builder.UseEnvironment(environment));

            using var client = configuredFactory.CreateClient();

            using var response = await client.GetAsync("/openapi/v1.json");

            Assert.Equal(expectedStatus, response.StatusCode);

            if (expectedStatus == HttpStatusCode.OK)
            {
                var document = await response.Content
                    .ReadFromJsonAsync<JsonElement>();

                var operation = document.GetProperty("paths")
                    .GetProperty(Endpoint)
                    .GetProperty("post");

                var responses = operation.GetProperty("responses");

                foreach (var status in new[] { "200", "400", "404", "409", "500" })
                {
                    Assert.True(
                        responses.TryGetProperty(status, out _),
                        $"OpenAPI is missing response {status}.");
                }
            }
        }
    }
}