using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockFlow.API.Contracts.PurchaseOrders;
using StockFlow.Application.Authorization;
using StockFlow.Application.PurchaseOrders;
using StockFlow.Application.PurchaseOrders.ApprovePurchaseOrder;
using StockFlow.Application.PurchaseOrders.CreatePurchaseOrder;
using StockFlow.Application.PurchaseOrders.GetPurchaseOrderById;
using StockFlow.Application.PurchaseOrders.GetPurchaseOrders;
using StockFlow.Application.PurchaseOrders.RejectPurchaseOrder;
using StockFlow.Application.PurchaseOrders.SubmitPurchaseOrder;
using StockFlow.Application.PurchaseOrders.UpdateDraftPurchaseOrder;

namespace StockFlow.API.Controllers
{
    [Route("api/purchase-orders")]
    [ApiController]
    public sealed class PurchaseOrdersController : ControllerBase
    {
        private readonly CreatePurchaseOrderCommandHandler _createHandler;
        private readonly GetPurchaseOrderByIdQueryHandler _detailsHandler;
        private readonly GetPurchaseOrdersQueryHandler _listHandler;
        private readonly UpdateDraftPurchaseOrderCommandHandler _updateHandler;
        private readonly SubmitPurchaseOrderCommandHandler _submitHandler;
        private readonly ApprovePurchaseOrderCommandHandler _approveHandler;
        private readonly RejectPurchaseOrderCommandHandler _rejectHandler;

        public PurchaseOrdersController(
            CreatePurchaseOrderCommandHandler createHandler,
            GetPurchaseOrderByIdQueryHandler detailsHandler,
            GetPurchaseOrdersQueryHandler listHandler,
            UpdateDraftPurchaseOrderCommandHandler updateHandler,
            SubmitPurchaseOrderCommandHandler submitHandler,
            ApprovePurchaseOrderCommandHandler approveHandler,
            RejectPurchaseOrderCommandHandler rejectHandler)
        {
            ArgumentNullException.ThrowIfNull(createHandler);
            ArgumentNullException.ThrowIfNull(detailsHandler);
            ArgumentNullException.ThrowIfNull(listHandler);
            ArgumentNullException.ThrowIfNull(updateHandler);
            ArgumentNullException.ThrowIfNull(submitHandler);
            ArgumentNullException.ThrowIfNull(approveHandler);
            ArgumentNullException.ThrowIfNull(rejectHandler);

            _createHandler = createHandler;
            _detailsHandler = detailsHandler;
            _listHandler = listHandler;
            _updateHandler = updateHandler;
            _submitHandler = submitHandler;
            _approveHandler = approveHandler;
            _rejectHandler = rejectHandler;
        }

        [HttpPost]
        [Authorize(Policy = StockFlowPolicies.PurchaseOrdersManage)]
        [ProducesResponseType(typeof(CreatePurchaseOrderResult), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<CreatePurchaseOrderResult>> Create(
            [FromBody] CreatePurchaseOrderRequest request,
            CancellationToken cancellationToken)
        {
            var lines = request.Lines!.Select(line =>
            {
                var validatedLine = line!;

                return new CreatePurchaseOrderLineCommand(
                    validatedLine.ProductId!.Value,
                    validatedLine.OrderedQuantity!.Value,
                    validatedLine.UnitPrice!.Value);
            }).ToArray();

            var command = new CreatePurchaseOrderCommand(
                request.SupplierId!.Value,
                request.WarehouseId!.Value,
                lines);

            var result = await _createHandler.HandleAsync(
                command, cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.PurchaseOrderId },
                result);
        }

        [HttpGet("{id:guid}")]
        [Authorize(Policy = StockFlowPolicies.PurchaseOrdersRead)]
        [ProducesResponseType(typeof(PurchaseOrderDetailsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PurchaseOrderDetailsDto>> GetById(
            Guid id,
            CancellationToken cancellationToken)
        {
            var result = await _detailsHandler.HandleAsync(
                new GetPurchaseOrderByIdQuery(id),
                cancellationToken);

            return Ok(result);
        }

        [HttpGet]
        [Authorize(Policy = StockFlowPolicies.PurchaseOrdersRead)]
        [ProducesResponseType(typeof(GetPurchaseOrdersResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<GetPurchaseOrdersResult>> GetPage(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var result = await _listHandler.HandleAsync(
                new GetPurchaseOrdersQuery(pageNumber, pageSize),
                cancellationToken);

            return Ok(result);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = StockFlowPolicies.PurchaseOrdersManage)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateDraft(
            Guid id,
            [FromBody] UpdateDraftPurchaseOrderRequest request,
            CancellationToken cancellationToken)
        {
            var lines = request.Lines!.Select(line =>
            {
                var validatedLine = line!;

                return new UpdateDraftPurchaseOrderLineCommand(
                    validatedLine.ProductId!.Value,
                    validatedLine.OrderedQuantity!.Value,
                    validatedLine.UnitPrice!.Value);
            }).ToArray();

            var command = new UpdateDraftPurchaseOrderCommand(
                id,
                request.ExpectedVersion!.Value,
                lines);

            await _updateHandler.HandleAsync(command, cancellationToken);
            return NoContent();
        }

        [HttpPost("{id:guid}/submit")]
        [Authorize(Policy = StockFlowPolicies.PurchaseOrdersManage)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Submit(
            Guid id,
            [FromBody] PurchaseOrderVersionRequest request,
            CancellationToken cancellationToken)
        {
            await _submitHandler.HandleAsync(
                new SubmitPurchaseOrderCommand(id, request.ExpectedVersion!.Value),
                cancellationToken);

            return NoContent();
        }

        [HttpPost("{id:guid}/approve")]
        [Authorize(Policy = StockFlowPolicies.PurchaseOrdersApprove)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Approve(
            Guid id,
            [FromBody] PurchaseOrderVersionRequest request,
            CancellationToken cancellationToken)
        {
            await _approveHandler.HandleAsync(
                new ApprovePurchaseOrderCommand(id, request.ExpectedVersion!.Value),
                cancellationToken);

            return NoContent();
        }

        [HttpPost("{id:guid}/reject")]
        [Authorize(Policy = StockFlowPolicies.PurchaseOrdersApprove)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Reject(
            Guid id,
            [FromBody] PurchaseOrderVersionRequest request,
            CancellationToken cancellationToken)
        {
            await _rejectHandler.HandleAsync(
                new RejectPurchaseOrderCommand(id, request.ExpectedVersion!.Value),
                cancellationToken);

            return NoContent();
        }
    }
}
