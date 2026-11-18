using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StockFlow.Application.Authorization;
using StockFlow.Application.PurchasingNotifications.GetPurchasingNotifications;
using StockFlow.Application.PurchasingNotifications.MarkPurchasingNotificationAsRead;

namespace StockFlow.API.Controllers
{
    [Route("api/purchasing-notifications")]
    [ApiController]
    [Authorize(Policy = StockFlowPolicies.PurchaseOrdersRead)]
    public sealed class PurchasingNotificationsController : ControllerBase
    {
        private readonly GetPurchasingNotificationsQueryHandler _inboxHandler;
        private readonly MarkPurchasingNotificationAsReadCommandHandler _markAsReadHandler;

        public PurchasingNotificationsController(
            GetPurchasingNotificationsQueryHandler inboxHandler,
            MarkPurchasingNotificationAsReadCommandHandler markAsReadHandler)
        {
            ArgumentNullException.ThrowIfNull(inboxHandler);
            ArgumentNullException.ThrowIfNull(markAsReadHandler);

            _inboxHandler = inboxHandler;
            _markAsReadHandler = markAsReadHandler;
        }

        [HttpGet]
        [ProducesResponseType(typeof(GetPurchasingNotificationsResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<GetPurchasingNotificationsResult>> GetInbox(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var result = await _inboxHandler.HandleAsync(
                new GetPurchasingNotificationsQuery(pageNumber, pageSize),
                cancellationToken);

            return Ok(result);
        }

        [HttpPut("{id:guid}/read")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MarkAsRead(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            await _markAsReadHandler.HandleAsync(
                new MarkPurchasingNotificationAsReadCommand(id),
                cancellationToken);

            return NoContent();
        }
    }
}
