using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockFlow.API.Contracts.Transfers;
using StockFlow.Application.Authorization;
using StockFlow.Application.Transfers;
using StockFlow.Application.Transfers.CancelDraftTransfer;
using StockFlow.Application.Transfers.CreateTransfer;
using StockFlow.Application.Transfers.DispatchTransfer;
using StockFlow.Application.Transfers.GetTransferById;
using StockFlow.Application.Transfers.GetTransferReceipts;
using StockFlow.Application.Transfers.GetTransfers;
using StockFlow.Application.Transfers.ReceiveTransfer;
using StockFlow.Application.Transfers.UpdateDraftTransfer;
using StockFlow.Domain.Enums;

namespace StockFlow.API.Controllers
{
    [Route("api/transfers")]
    [ApiController]
    public sealed class TransfersController : ControllerBase
    {
        private readonly CreateTransferCommandHandler _createHandler;
        private readonly UpdateDraftTransferCommandHandler _updateHandler;
        private readonly CancelDraftTransferCommandHandler _cancelHandler;
        private readonly DispatchTransferCommandHandler _dispatchHandler;
        private readonly ReceiveTransferCommandHandler _receiveHandler;
        private readonly GetTransfersQueryHandler _listHandler;
        private readonly GetTransferByIdQueryHandler _detailsHandler;
        private readonly GetTransferReceiptsQueryHandler _receiptHistoryHandler;

        public TransfersController(
            CreateTransferCommandHandler createHandler,
            UpdateDraftTransferCommandHandler updateHandler,
            CancelDraftTransferCommandHandler cancelHandler,
            DispatchTransferCommandHandler dispatchHandler,
            ReceiveTransferCommandHandler receiveHandler,
            GetTransfersQueryHandler listHandler,
            GetTransferByIdQueryHandler detailsHandler,
            GetTransferReceiptsQueryHandler receiptHistoryHandler)
        {
            ArgumentNullException.ThrowIfNull(createHandler);
            ArgumentNullException.ThrowIfNull(updateHandler);
            ArgumentNullException.ThrowIfNull(cancelHandler);
            ArgumentNullException.ThrowIfNull(dispatchHandler);
            ArgumentNullException.ThrowIfNull(receiveHandler);
            ArgumentNullException.ThrowIfNull(listHandler);
            ArgumentNullException.ThrowIfNull(detailsHandler);
            ArgumentNullException.ThrowIfNull(receiptHistoryHandler);

            _createHandler = createHandler;
            _updateHandler = updateHandler;
            _cancelHandler = cancelHandler;
            _dispatchHandler = dispatchHandler;
            _receiveHandler = receiveHandler;
            _listHandler = listHandler;
            _detailsHandler = detailsHandler;
            _receiptHistoryHandler = receiptHistoryHandler;
        }

        [HttpGet("{id:guid}")]
        [Authorize(Policy = StockFlowPolicies.TransfersRead)]
        [ProducesResponseType(typeof(TransferDetailsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<TransferDetailsDto>> GetById(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            var result = await _detailsHandler.HandleAsync(
                new GetTransferByIdQuery(id),
                cancellationToken);

            return Ok(result);
        }

        [HttpGet("{id:guid}/receipts")]
        [Authorize(Policy = StockFlowPolicies.TransfersRead)]
        [ProducesResponseType(
            typeof(IReadOnlyList<TransferReceiptHistoryItemDto>),
            StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IReadOnlyList<TransferReceiptHistoryItemDto>>> GetReceipts(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            var result = await _receiptHistoryHandler.HandleAsync(
                new GetTransferReceiptsQuery(id),
                cancellationToken);

            return Ok(result);
        }

        [HttpGet]
        [Authorize(Policy = StockFlowPolicies.TransfersRead)]
        [ProducesResponseType(typeof(GetTransfersResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<GetTransfersResult>> GetPage(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] TransferStatus? status = null,
            [FromQuery] string? search = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _listHandler.HandleAsync(
                new GetTransfersQuery(pageNumber, pageSize, status, search),
                cancellationToken);

            return Ok(result);
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

        [HttpPost("{id:guid}/dispatch")]
        [Authorize(Policy = StockFlowPolicies.TransfersDispatch)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Dispatch(
            [FromRoute] Guid id,
            [FromBody] DispatchTransferRequest request,
            CancellationToken cancellationToken)
        {
            var command = new DispatchTransferCommand(
                id, request.ExpectedVersion!.Value);

            await _dispatchHandler.HandleAsync(command, cancellationToken);

            return NoContent();
        }

        [HttpPost("{id:guid}/receipts")]
        [Authorize(Policy = StockFlowPolicies.TransfersReceive)]
        [ProducesResponseType(typeof(ReceiveTransferResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<ReceiveTransferResult>> Receive(
            [FromRoute] Guid id,
            [FromBody] ReceiveTransferRequest request,
            CancellationToken cancellationToken)
        {
            var lines = request.Lines!.Select(line =>
            {
                var validatedLine = line!;

                return new ReceiveTransferLineCommand(
                    validatedLine.TransferLineId!.Value,
                    validatedLine.ReceivedQuantity!.Value);
            }).ToArray();

            var command = new ReceiveTransferCommand(
                request.ReceiptId!.Value,
                id,
                request.ExpectedVersion!.Value,
                lines);

            var result = await _receiveHandler.HandleAsync(
                command, cancellationToken);

            return Ok(result);
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
    }
}
