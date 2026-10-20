using Microsoft.AspNetCore.Mvc;
using StockFlow.API.Contracts.Suppliers;
using StockFlow.Application.Suppliers;
using StockFlow.Application.Suppliers.ActivateSupplier;
using StockFlow.Application.Suppliers.CreateSupplier;
using StockFlow.Application.Suppliers.DeactivateSupplier;
using StockFlow.Application.Suppliers.GetSupplierById;
using StockFlow.Application.Suppliers.GetSuppliers;
using StockFlow.Application.Suppliers.UpdateSupplier;

namespace StockFlow.API.Controllers
{
    [Route("api/suppliers")]
    [ApiController]
    public class SuppliersController : ControllerBase
    {
        private readonly CreateSupplierCommandHandler _createHandler;
        private readonly GetSuppliersQueryHandler _queryHandler;
        private readonly GetSupplierByIdQueryHandler _detailsHandler;
        private readonly UpdateSupplierCommandHandler _updateHandler;
        private readonly ActivateSupplierCommandHandler _activateHandler;
        private readonly DeactivateSupplierCommandHandler _deactivateHandler;

        public SuppliersController(
            CreateSupplierCommandHandler createHandler,
            GetSuppliersQueryHandler queryHandler,
            GetSupplierByIdQueryHandler detailsQuery,
            UpdateSupplierCommandHandler updateHandler,
            ActivateSupplierCommandHandler activateHandler,
            DeactivateSupplierCommandHandler deactivateHandler)
        {
            ArgumentNullException.ThrowIfNull(createHandler);
            ArgumentNullException.ThrowIfNull(queryHandler);
            ArgumentNullException.ThrowIfNull(detailsQuery);
            ArgumentNullException.ThrowIfNull(updateHandler);
            ArgumentNullException.ThrowIfNull(activateHandler);
            ArgumentNullException.ThrowIfNull(deactivateHandler);

            _createHandler = createHandler;
            _queryHandler = queryHandler;
            _detailsHandler = detailsQuery;
            _updateHandler = updateHandler;
            _activateHandler = activateHandler;
            _deactivateHandler = deactivateHandler;
        }

        [HttpGet]
        [ProducesResponseType(
            typeof(GetSuppliersResult),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<GetSuppliersResult>> Get(
            [FromQuery] GetSuppliersRequest request,
            CancellationToken cancellationToken)
        {
            var query = new GetSuppliersQuery(
                request.PageNumber,
                request.PageSize,
                request.Search,
                request.IsActive);

            var result = await _queryHandler.HandleAsync(
                query,
                cancellationToken);

            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(
            typeof(SupplierDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status404NotFound)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<SupplierDto>> GetById(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            var result = await _detailsHandler.HandleAsync(
                new GetSupplierByIdQuery(id),
                cancellationToken);

            return Ok(result);
        }

        [HttpPost]
        [ProducesResponseType(
            typeof(CreateSupplierResult),
            StatusCodes.Status201Created)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status409Conflict)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<CreateSupplierResult>> Create(
            [FromBody] CreateSupplierRequest request,
            CancellationToken cancellationToken)
        {
            var command = new CreateSupplierCommand(
                request.Code!,
                request.Name!,
                request.ContactEmail!,
                request.PhoneNumber);

            var result = await _createHandler.HandleAsync(
                command,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.SupplierId },
                result);
        }

        [HttpPut("{id:guid}")]
        [ProducesResponseType(
            typeof(SupplierDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status404NotFound)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<SupplierDto>> Update(
            [FromRoute] Guid id,
            [FromBody] UpdateSupplierRequest request,
            CancellationToken cancellationToken)
        {
            var command = new UpdateSupplierCommand(
                id,
                request.Name!,
                request.ContactEmail!,
                request.PhoneNumber);

            var result = await _updateHandler.HandleAsync(
                command,
                cancellationToken);

            return Ok(result);
        }

        [HttpPatch("{id:guid}/activate")]
        [ProducesResponseType(
            typeof(SupplierDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status404NotFound)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<SupplierDto>> Activate(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            var result = await _activateHandler.HandleAsync(
                new ActivateSupplierCommand(id),
                cancellationToken);

            return Ok(result);
        }

        [HttpPatch("{id:guid}/deactivate")]
        [ProducesResponseType(
            typeof(SupplierDto),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status404NotFound)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<SupplierDto>> Deactivate(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            var result = await _deactivateHandler.HandleAsync(
                new DeactivateSupplierCommand(id),
                cancellationToken);

            return Ok(result);
        }
    }
}
