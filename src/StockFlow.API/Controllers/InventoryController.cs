using Microsoft.AspNetCore.Mvc;
using StockFlow.API.Contracts.Inventory;
using StockFlow.Application.Inventory.GetInventory;
using StockFlow.Application.Inventory.ReceiveStock;

namespace StockFlow.API.Controllers
{
    [Route("api/inventory")]
    [ApiController]
    public sealed class InventoryController : ControllerBase
    {
        private readonly ReceiveStockCommandHandler _handler;
        private readonly GetInventoryQueryHandler _queryHandler;

        public InventoryController(
            ReceiveStockCommandHandler handler,
            GetInventoryQueryHandler queryHandler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            ArgumentNullException.ThrowIfNull(queryHandler);

            _handler = handler;
            _queryHandler = queryHandler;
        }

        [HttpGet]
        [ProducesResponseType(
            typeof(GetInventoryResult),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<GetInventoryResult>> Get(
            [FromQuery] GetInventoryRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _queryHandler.HandleAsync(
                new GetInventoryQuery(
                    request.PageNumber,
                    request.PageSize),
                cancellationToken);

            return Ok(result);
        }

        [HttpPost("receive")]
        [ProducesResponseType(
            typeof(ReceiveStockResult),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status404NotFound)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status409Conflict)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ReceiveStockResult>> Receive(
            [FromBody] ReceiveStockRequest request,
            CancellationToken cancellationToken)
        {
            var command = new ReceiveStockCommand(
                request.ProductId!.Value,
                request.WarehouseId!.Value,
                request.Quantity!.Value);

            var result = await _handler.HandleAsync(
                command,
                cancellationToken);

            return Ok(result);
        }
    }
}
