using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StockFlow.API.Antiforgery;
using StockFlow.API.Contracts.Authentication;
using StockFlow.API.RateLimiting;
using StockFlow.Application.Abstractions.Authentication;
using StockFlow.Application.Authentication;
using StockFlow.Application.Authorization;
using System.Security.Claims;

namespace StockFlow.API.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public sealed class AuthController : ControllerBase
    {
        private readonly IAntiforgery _antiforgery;
        private readonly IAccountService _account;
        private readonly IAuthorizationService _authorization;
        public AuthController(
            IAntiforgery antiforgery,
            IAccountService account,
            IAuthorizationService authorization)
        {
            ArgumentNullException.ThrowIfNull(antiforgery);
            ArgumentNullException.ThrowIfNull(account);
            ArgumentNullException.ThrowIfNull(authorization);

            _antiforgery = antiforgery;
            _account = account;
            _authorization = authorization;
        }

        [AllowAnonymous]
        [HttpGet("csrf")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [ProducesResponseType(
            typeof(AntiforgeryTokenResponse),
            StatusCodes.Status200OK)]
        public ActionResult<AntiforgeryTokenResponse> GetAntiforgeryToken()
        {
            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);

            var requestToken = tokens.RequestToken
                ?? throw new InvalidOperationException(
                    "An antiforgery request token could not be generated.");

            return Ok(new AntiforgeryTokenResponse(
                requestToken,
                AntiforgeryRegistration.HeaderName));
        }

        [AllowAnonymous]
        [HttpPost("login")]
        [EnableRateLimiting(RateLimitingRegistration.LoginPolicy)]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ValidationProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<UserProfileDto>> Login(
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken)
        {
            var profile = await _account.SignInAsync(
                request.Email!,
                request.Password!,
                cancellationToken);

            if (profile is null)
            {
                return Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Sign-in failed",
                    detail: "Invalid email or password.");
            }

            return Ok(profile);
        }

        [Authorize]
        [HttpGet("me")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [ProducesResponseType(
            typeof(CurrentUserResponse),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<CurrentUserResponse>> GetCurrentUser(
            CancellationToken cancellationToken)
        {
            var identifier = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(identifier, out var userId) ||
                userId == Guid.Empty)
            {
                return Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Authentication required");
            }

            var profile = await _account.GetProfileAsync(
                userId,
                cancellationToken);

            if (profile is null)
            {
                return Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Authentication required");
            }

            var policies = new[]
            {
                StockFlowPolicies.CatalogRead,
                StockFlowPolicies.InventoryRead,
                StockFlowPolicies.InventoryReceive,
                StockFlowPolicies.SuppliersRead,
                StockFlowPolicies.SuppliersManage,
                StockFlowPolicies.BackgroundJobsManage,
            };

            var capabilities = new List<string>();

            foreach (var policy in policies)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await _authorization.AuthorizeAsync(
                    User,
                    resource: null,
                    policyName: policy);

                if (result.Succeeded)
                {
                    capabilities.Add(policy);
                }
            }

            return Ok(new CurrentUserResponse(
                profile.Id,
                profile.Email,
                profile.DisplayName,
                profile.Roles,
                capabilities.ToArray()));
        }

        [Authorize]
        [HttpPost("logout")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Logout(
            CancellationToken cancellationToken)
        {
            var identifier = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(identifier, out var userId) ||
                userId == Guid.Empty)
            {
                return Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Authentication required");
            }

            await _account.SignOutAsync(userId, cancellationToken);

            return NoContent();
        }
    }
}
