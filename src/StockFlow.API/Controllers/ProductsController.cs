using Microsoft.AspNetCore.Mvc;
using StockFlow.Application.Products.GetProducts;

namespace StockFlow.API.Controllers
{
    [Route("api/products")]
    [ApiController]
    public sealed class ProductsController : ControllerBase
    {
        private readonly GetProductsQueryHandler _handler;

        public ProductsController(GetProductsQueryHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            _handler = handler;
        }

        [HttpGet]
        [ProducesResponseType(
            typeof(IReadOnlyList<ProductDto>),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IReadOnlyList<ProductDto>>> Get(
            CancellationToken cancellationToken)
        {
            var products = await _handler.HandleAsync(
                new GetProductsQuery(),
                cancellationToken);

            return Ok(products);
        }
    }
}
