using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StockFlow.Application.Products.GetProducts;

namespace StockFlow.API.Tests
{
    public class ProductsEndpointTests
    {
        [Fact]
        public async Task GetProducts_ReturnsProductDtos()
        {
            using var factory = new StockFlowApiFactory();

            var products = new[]
            {
                new ProductDto(
                    Guid.NewGuid(),
                    "Synthetic test chair",
                    "TEST-CHAIR",
                    "Synthetic test description",
                    true),
                new ProductDto(
                    Guid.NewGuid(),
                    "Synthetic inactive product",
                    "TEST-INACTIVE",
                    null,
                    false)
            };

            factory.ProductReader.Products = products;

            using var client = factory.CreateClient();
            using var response = await client.GetAsync("/api/products");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content
                .ReadFromJsonAsync<ProductDto[]>();

            Assert.NotNull(result);
            Assert.Equal(products, result);
            Assert.Equal(1, factory.ProductReader.CallCount);
            Assert.Equal(0, factory.Inventories.SaveCallCount);
        }

        [Fact]
        public async Task GetProducts_WhenCatalogIsEmpty_ReturnsEmptyArray()
        {
            using var factory = new StockFlowApiFactory();
            using var client = factory.CreateClient();

            using var response = await client.GetAsync("/api/products");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content
                .ReadFromJsonAsync<ProductDto[]>();

            Assert.NotNull(result);
            Assert.Empty(result);
            Assert.Equal(1, factory.ProductReader.CallCount);
        }

        [Fact]
        public async Task GetProducts_WhenReadingFails_ReturnsSafeProblemDetails()
        {
            using var factory = new StockFlowApiFactory();

            const string internalMessage =
                "Sensitive product query implementation details.";

            factory.ProductReader.ReadException =
                new InvalidOperationException(internalMessage);

            using var client = factory.CreateClient();
            using var response = await client.GetAsync("/api/products");

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