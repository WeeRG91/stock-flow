using Microsoft.AspNetCore.Mvc;
using StockFlow.API.Contracts.Inventory;
using StockFlow.Application.Inventory.ReceiveStock;

namespace StockFlow.API.Controllers
{
    [Route("api/inventory")]
    [ApiController]
    public sealed class InventoryController : ControllerBase
    {
        private readonly ReceiveStockCommandHandler _handler;

        public InventoryController(ReceiveStockCommandHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            _handler = handler;
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
