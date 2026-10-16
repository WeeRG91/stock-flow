using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StockFlow.Application.Warehouses.GetWarehouses;

namespace StockFlow.API.Tests
{
    public class WarehousesEndpointTests
    {
        [Fact]
        public async Task GetWarehouses_ReturnsWarehouseDtos()
        {
            using var factory = new StockFlowApiFactory();

            var warehouses = new[]
            {
                new WarehouseDto(
                    Guid.NewGuid(),
                    "TEST-BRU",
                    "Synthetic Brussels warehouse",
                    "Brussels",
                    true),
                new WarehouseDto(
                    Guid.NewGuid(),
                    "TEST-LUX",
                    "Synthetic inactive warehouse",
                    "Luxembourg",
                    false)
            };

            factory.WarehouseReader.Warehouses = warehouses;

            using var client = factory.CreateClient();
            using var response = await client.GetAsync("/api/warehouses");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content
                .ReadFromJsonAsync<WarehouseDto[]>();

            Assert.NotNull(result);
            Assert.Equal(warehouses, result);
            Assert.Equal(1, factory.WarehouseReader.CallCount);
            Assert.Equal(0, factory.Inventories.SaveCallCount);
        }

        [Fact]
        public async Task GetWarehouses_WhenEmpty_ReturnsEmptyArray()
        {
            using var factory = new StockFlowApiFactory();
            using var client = factory.CreateClient();

            using var response = await client.GetAsync("/api/warehouses");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content
                .ReadFromJsonAsync<WarehouseDto[]>();

            Assert.NotNull(result);
            Assert.Empty(result);
            Assert.Equal(1, factory.WarehouseReader.CallCount);
            Assert.Equal(0, factory.Inventories.SaveCallCount);
        }

        [Fact]
        public async Task GetWarehouses_WhenReadingFails_ReturnsSafeProblemDetails()
        {
            using var factory = new StockFlowApiFactory();

            const string internalMessage =
                "Sensitive warehouse query implementation details.";

            factory.WarehouseReader.ReadException =
                new InvalidOperationException(internalMessage);

            using var client = factory.CreateClient();
            using var response = await client.GetAsync("/api/warehouses");

            Assert.Equal(
                HttpStatusCode.InternalServerError,
                response.StatusCode);

            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);

            var problem = await response.Content
                .ReadFromJsonAsync<JsonElement>();

            Assert.Equal(500, problem.GetProperty("status").GetInt32());
            Assert.Equal(
                "An unexpected error occurred.",
                problem.GetProperty("detail").GetString());

            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(internalMessage, body);
            Assert.Equal(0, factory.Inventories.SaveCallCount);
        }
    }
}