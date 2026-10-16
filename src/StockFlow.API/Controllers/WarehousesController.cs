using Microsoft.AspNetCore.Mvc;
using StockFlow.Application.Warehouses.GetWarehouses;

namespace StockFlow.API.Controllers
{
    [Route("api/warehouses")]
    [ApiController]
    public sealed class WarehousesController : ControllerBase
    {
        private readonly GetWarehousesQueryHandler _handler;

        public WarehousesController(GetWarehousesQueryHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            _handler = handler;
        }

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
    }
}
