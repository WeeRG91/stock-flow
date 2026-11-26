using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockFlow.API.Contracts.StockReservations;
using StockFlow.Application.Authorization;
using StockFlow.Application.StockReservations;
using StockFlow.Application.StockReservations.CreateStockReservation;
using StockFlow.Application.StockReservations.FulfillStockReservation;
using StockFlow.Application.StockReservations.GetStockReservationById;
using StockFlow.Application.StockReservations.GetStockReservations;
using StockFlow.Application.StockReservations.ReleaseStockReservation;

namespace StockFlow.API.Controllers
{
    [Route("api/stock-reservations")]
    [ApiController]
    public class StockReservationsController : ControllerBase
    {
        private readonly CreateStockReservationCommandHandler _createHandler;
        private readonly ReleaseStockReservationCommandHandler _releaseHandler;
        private readonly FulfillStockReservationCommandHandler _fulfillHandler;
        private readonly GetStockReservationByIdQueryHandler _detailsHandler;
        private readonly GetStockReservationsQueryHandler _listHandler;

        public StockReservationsController(
            CreateStockReservationCommandHandler createHandler,
            ReleaseStockReservationCommandHandler releaseHandler,
            FulfillStockReservationCommandHandler fulfillHandler,
            GetStockReservationByIdQueryHandler detailsHandler,
            GetStockReservationsQueryHandler listHandler)
        {
            ArgumentNullException.ThrowIfNull(createHandler);
            ArgumentNullException.ThrowIfNull(releaseHandler);
            ArgumentNullException.ThrowIfNull(fulfillHandler);
            ArgumentNullException.ThrowIfNull(detailsHandler);
            ArgumentNullException.ThrowIfNull(listHandler);

            _createHandler = createHandler;
            _releaseHandler = releaseHandler;
            _fulfillHandler = fulfillHandler;
            _detailsHandler = detailsHandler;
            _listHandler = listHandler;
        }

        [HttpGet]
        [Authorize(Policy = StockFlowPolicies.InventoryRead)]
        [ProducesResponseType(
            typeof(GetStockReservationsResult),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<GetStockReservationsResult>> Get(
            [FromQuery] GetStockReservationsRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _listHandler.HandleAsync(
                new GetStockReservationsQuery(
                    request.PageNumber,
                    request.PageSize,
                    request.Status,
                    request.Search,
                    request.InventoryId),
                cancellationToken);

            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        [Authorize(Policy = StockFlowPolicies.InventoryRead)]
        [ProducesResponseType(typeof(StockReservationDto), StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<StockReservationDto>> GetById(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            var result = await _detailsHandler.HandleAsync(
                new GetStockReservationByIdQuery(id),
                cancellationToken);

            return Ok(result);
        }

        [HttpPost]
        [Authorize(Policy = StockFlowPolicies.StockReservationsManage)]
        [ProducesResponseType(
            typeof(CreateStockReservationResult),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<CreateStockReservationResult>> Create(
            [FromBody] CreateStockReservationRequest request,
            CancellationToken cancellationToken)
        {
            var command = new CreateStockReservationCommand(
                request.RequestId!.Value,
                request.InventoryId!.Value,
                request.Quantity!.Value,
                request.Reference!);

            var result = await _createHandler.HandleAsync(
                command,
                cancellationToken);

            return Ok(result);
        }

        [HttpPost("{id:guid}/release")]
        [Authorize(Policy = StockFlowPolicies.StockReservationsManage)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Release(
            [FromRoute] Guid id,
            [FromBody] StockReservationVersionRequest request,
            CancellationToken cancellationToken)
        {
            await _releaseHandler.HandleAsync(
                new ReleaseStockReservationCommand(
                    id,
                    request.ExpectedVersion!.Value),
                cancellationToken);

            return NoContent();
        }

        [HttpPost("{id:guid}/fulfill")]
        [Authorize(Policy = StockFlowPolicies.StockReservationsFulfill)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Fulfill(
            [FromRoute] Guid id,
            [FromBody] StockReservationVersionRequest request,
            CancellationToken cancellationToken)
        {
            await _fulfillHandler.HandleAsync(
                new FulfillStockReservationCommand(
                    id,
                    request.ExpectedVersion!.Value),
                cancellationToken);

            return NoContent();
        }
    }
}
