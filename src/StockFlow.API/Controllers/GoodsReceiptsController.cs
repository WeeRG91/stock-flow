using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockFlow.API.Contracts.PurchaseOrders;
using StockFlow.Application.Authorization;
using StockFlow.Application.GoodsReceipts.ConfirmGoodsReceipt;
using StockFlow.Application.GoodsReceipts.GetGoodsReceipts;

namespace StockFlow.API.Controllers
{
    [Route("api/purchase-orders/{id:guid}/receipts")]
    [ApiController]
    public sealed class GoodsReceiptsController : ControllerBase
    {
        private readonly ConfirmGoodsReceiptCommandHandler _handler;
        private readonly GetGoodsReceiptQueryHandler _historyHandler;

        public GoodsReceiptsController(
            ConfirmGoodsReceiptCommandHandler handler,
            GetGoodsReceiptQueryHandler historyHandler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            ArgumentNullException.ThrowIfNull(historyHandler);

            _handler = handler;
            _historyHandler = historyHandler;
        }

        [HttpGet]
        [Authorize(Policy = StockFlowPolicies.PurchaseOrdersRead)]
        [ProducesResponseType(typeof(IReadOnlyList<GoodsReceiptHistoryItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IReadOnlyList<GoodsReceiptHistoryItemDto>>> GetHistory(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            var receipts = await _historyHandler.HandleAsync(
                new GetGoodsReceiptQuery(id), cancellationToken);

            return Ok(receipts);
        }

        [HttpPost]
        [Authorize(Policy = StockFlowPolicies.InventoryReceive)]
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
