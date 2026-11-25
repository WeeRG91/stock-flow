using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockFlow.Application.Authorization;
using StockFlow.Application.Products.GetProductById;
using StockFlow.Application.Products.GetProducts;

namespace StockFlow.API.Controllers
{
    [Route("api/products")]
    [ApiController]
    public sealed class ProductsController : ControllerBase
    {
        private readonly GetProductsQueryHandler _handler;
        private readonly GetProductByIdQueryHandler _detailsHandler;

        public ProductsController(
            GetProductsQueryHandler handler,
            GetProductByIdQueryHandler detailsHandler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            ArgumentNullException.ThrowIfNull(detailsHandler);

            _handler = handler;
            _detailsHandler = detailsHandler;
        }

        [Authorize(Policy = StockFlowPolicies.CatalogRead)]
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

        [Authorize(Policy = StockFlowPolicies.CatalogRead)]
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ProductDto>> GetById(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            var product = await _detailsHandler.HandleAsync(
                new GetProductByIdQuery(id),
                cancellationToken);

            return Ok(product);
        }
    }
}
