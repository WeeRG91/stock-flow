using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockFlow.API.Contracts.PurchaseOrders;
using StockFlow.Application.Authorization;
using StockFlow.Application.GoodsReceipts.ConfirmGoodsReceipt;

namespace StockFlow.API.Controllers
{
    [Route("api/purchase-orders/{id:guid}/receipts")]
    [ApiController]
    [Authorize(Policy = StockFlowPolicies.InventoryReceive)]
    public sealed class GoodsReceiptsController : ControllerBase
    {
        private readonly ConfirmGoodsReceiptCommandHandler _handler;

        public GoodsReceiptsController(ConfirmGoodsReceiptCommandHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            _handler = handler;
        }

        [HttpPost]
        [ProducesResponseType(typeof(ConfirmGoodsReceiptResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ConfirmGoodsReceiptResult>> Confirm(
            [FromRoute] Guid id,
            [FromBody] ConfirmGoodsReceiptRequest request,
            CancellationToken cancellationToken)
        {
            var lines = request.Lines!.Select(line =>
            {
                var validatedLine = line!;

                return new ConfirmGoodsReceiptLineCommand(
                    validatedLine.PurchaseOrderLineId!.Value,
                    validatedLine.ReceivedQuantity!.Value);
            }).ToArray();

            var command = new ConfirmGoodsReceiptCommand(
                request.ReceiptId!.Value,
                id,
                request.ExpectedVersion!.Value,
                lines);

            var result = await _handler.HandleAsync(command, cancellationToken);
            return Ok(result);
        }
    }
}
