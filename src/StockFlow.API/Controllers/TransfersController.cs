using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockFlow.API.Contracts.Transfers;
using StockFlow.Application.Authorization;
using StockFlow.Application.Transfers.CancelDraftTransfer;
using StockFlow.Application.Transfers.CreateTransfer;
using StockFlow.Application.Transfers.UpdateDraftTransfer;

namespace StockFlow.API.Controllers
{
    [Route("api/transfers")]
    [ApiController]
    public sealed class TransfersController : ControllerBase
    {
        private readonly CreateTransferCommandHandler _createHandler;
        private readonly UpdateDraftTransferCommandHandler _updateHandler;
        private readonly CancelDraftTransferCommandHandler _cancelHandler;

        public TransfersController(
            CreateTransferCommandHandler createHandler,
            UpdateDraftTransferCommandHandler updateHandler,
            CancelDraftTransferCommandHandler cancelHandler)
        {
            ArgumentNullException.ThrowIfNull(createHandler);
            ArgumentNullException.ThrowIfNull(updateHandler);
            ArgumentNullException.ThrowIfNull(cancelHandler);

            _createHandler = createHandler;
            _updateHandler = updateHandler;
            _cancelHandler = cancelHandler;
        }

        [HttpPost]
        [Authorize(Policy = StockFlowPolicies.TransfersManage)]
        [ProducesResponseType(typeof(CreateTransferResult), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<CreateTransferResult>> Create(
            [FromBody] CreateTransferRequest request,
            CancellationToken cancellationToken)
        {
            var lines = request.Lines!.Select(line =>
            {
                var validatedLine = line!;

                return new CreateTransferLineCommand(
                    validatedLine.ProductId!.Value,
                    validatedLine.Quantity!.Value);
            }).ToArray();

            var command = new CreateTransferCommand(
                request.SourceWarehouseId!.Value,
                request.DestinationWarehouseId!.Value,
                lines);

            var result = await _createHandler.HandleAsync(
                command, cancellationToken);

            return StatusCode(StatusCodes.Status201Created, result);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = StockFlowPolicies.TransfersManage)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateDraft(
            [FromRoute] Guid id,
            [FromBody] UpdateDraftTransferRequest request,
            CancellationToken cancellationToken)
        {
            var lines = request.Lines!.Select(line =>
            {
                var validatedLine = line!;

                return new UpdateDraftTransferLineCommand(
                    validatedLine.ProductId!.Value,
                    validatedLine.Quantity!.Value);
            }).ToArray();

            var command = new UpdateDraftTransferCommand(
                id, request.ExpectedVersion!.Value, lines);

            await _updateHandler.HandleAsync(command, cancellationToken);
            return NoContent();
        }

        [HttpPost("{id:guid}/cancel")]
        [Authorize(Policy = StockFlowPolicies.TransfersManage)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CancelDraft(
            [FromRoute] Guid id,
            [FromBody] CancelDraftTransferRequest request,
            CancellationToken cancellationToken)
        {
            var command = new CancelDraftTransferCommand(
                id, request.ExpectedVersion!.Value);

            await _cancelHandler.HandleAsync(command, cancellationToken);

            return NoContent();
        }
    }
}
