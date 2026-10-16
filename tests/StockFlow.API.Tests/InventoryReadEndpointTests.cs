using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StockFlow.Application.Inventory.GetInventory;

namespace StockFlow.API.Tests
{
    public class InventoryReadEndpointTests
    {
        [Fact]
        public async Task GetInventory_ReturnsPageAndForwardsPagination()
        {
            using var factory = new StockFlowApiFactory();

            var item = new InventoryBalanceDto(
                Guid.NewGuid(),
                factory.Product.Id,
                factory.Product.Name,
                factory.Product.Sku,
                factory.Warehouse.Id,
                factory.Warehouse.Name,
                factory.Warehouse.Code,
                12);

            factory.InventoryReader.Items = new[] { item };
            factory.InventoryReader.TotalCount = 3;

            using var client = factory.CreateClient();
            using var response = await client.GetAsync(
                "/api/inventory?pageNumber=2&pageSize=1");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content
                .ReadFromJsonAsync<GetInventoryResult>();

            Assert.NotNull(result);
            Assert.Equal(item, Assert.Single(result.Items));
            Assert.Equal(2, result.PageNumber);
            Assert.Equal(1, result.PageSize);
            Assert.Equal(3, result.TotalCount);

            Assert.Equal(2, factory.InventoryReader.ReceivedPageNumber);
            Assert.Equal(1, factory.InventoryReader.ReceivedPageSize);
            Assert.Equal(1, factory.InventoryReader.CallCount);
            Assert.Equal(0, factory.Inventories.SaveCallCount);
        }

        [Theory]
        [InlineData("/api/inventory", 1, 0)]
        [InlineData("/api/inventory?pageNumber=9", 9, 4)]
        public async Task GetInventory_WithEmptyPage_ReturnsMetadata(
            string url,
            int expectedPage,
            int totalCount)
        {
            using var factory = new StockFlowApiFactory();
            factory.InventoryReader.TotalCount = totalCount;

            using var client = factory.CreateClient();
            using var response = await client.GetAsync(url);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content
                .ReadFromJsonAsync<GetInventoryResult>();

            Assert.NotNull(result);
            Assert.Empty(result.Items);
            Assert.Equal(expectedPage, result.PageNumber);
            Assert.Equal(20, result.PageSize);
            Assert.Equal(totalCount, result.TotalCount);
            Assert.Equal(1, factory.InventoryReader.CallCount);
        }

        [Theory]
        [InlineData("pageNumber=0")]
        [InlineData("pageNumber=-1")]
        [InlineData("pageSize=0")]
        [InlineData("pageSize=-1")]
        [InlineData("pageSize=101")]
        [InlineData("pageNumber=abc")]
        [InlineData("pageSize=abc")]
        [InlineData("pageNumber=2147483647&pageSize=100")]
        public async Task GetInventory_WithInvalidPagination_ReturnsValidationProblem(
            string queryString)
        {
            using var factory = new StockFlowApiFactory();
            using var client = factory.CreateClient();

            using var response = await client.GetAsync(
                $"/api/inventory?{queryString}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);

            var problem = await response.Content
                .ReadFromJsonAsync<JsonElement>();

            Assert.Equal(400, problem.GetProperty("status").GetInt32());
            Assert.True(
                problem.GetProperty("errors").EnumerateObject().Any());

            Assert.Equal(0, factory.InventoryReader.CallCount);
            Assert.Equal(0, factory.Inventories.SaveCallCount);
        }

        [Fact]
        public async Task GetInventory_WhenReadingFails_ReturnsSafeProblemDetails()
        {
            using var factory = new StockFlowApiFactory();

            const string internalMessage =
                "Sensitive inventory query implementation details.";

            factory.InventoryReader.ReadException =
                new InvalidOperationException(internalMessage);

            using var client = factory.CreateClient();
            using var response = await client.GetAsync("/api/inventory");

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