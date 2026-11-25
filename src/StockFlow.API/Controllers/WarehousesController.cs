using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockFlow.Application.Authorization;
using StockFlow.Application.Warehouses.GetWarehouseById;
using StockFlow.Application.Warehouses.GetWarehouses;

namespace StockFlow.API.Controllers
{
    [Route("api/warehouses")]
    [ApiController]
    public sealed class WarehousesController : ControllerBase
    {
        private readonly GetWarehousesQueryHandler _handler;
        private readonly GetWarehouseByIdQueryHandler _detailsHandler;

        public WarehousesController(
            GetWarehousesQueryHandler handler,
            GetWarehouseByIdQueryHandler detailsHandler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            ArgumentNullException.ThrowIfNull(detailsHandler);

            _handler = handler;
            _detailsHandler = detailsHandler;
        }

        [Authorize(Policy = StockFlowPolicies.CatalogRead)]
        [HttpGet]
        [ProducesResponseType(
            typeof(IReadOnlyList<WarehouseDto>),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IReadOnlyList<WarehouseDto>>> Get(
            CancellationToken cancellationToken)
        {
            var warehouses = await _handler.HandleAsync(
                new GetWarehousesQuery(),
                cancellationToken);

            return Ok(warehouses);
        }

        [Authorize(Policy = StockFlowPolicies.CatalogRead)]
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(WarehouseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<WarehouseDto>> GetById(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            var warehouse = await _detailsHandler.HandleAsync(
                new GetWarehouseByIdQuery(id),
                cancellationToken);

            return Ok(warehouse);
        }
    }
}
