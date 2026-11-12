using Microsoft.AspNetCore.Authorization;
using StockFlow.Application.Abstractions;
using System.Security.Claims;

namespace StockFlow.API.Authorization
{
    public sealed class HttpCurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IAuthorizationService _authorization;

        public HttpCurrentUser(
            IHttpContextAccessor httpContextAccessor,
            IAuthorizationService authorization)
        {
            ArgumentNullException.ThrowIfNull(httpContextAccessor);
            ArgumentNullException.ThrowIfNull(authorization);

            _httpContextAccessor = httpContextAccessor;
            _authorization = authorization;
        }

        public Guid? UserId
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;

                if (user?.Identity?.IsAuthenticated != true)
                {
                    return null;
                }

                var identifier = user.FindFirstValue(ClaimTypes.NameIdentifier);

                return Guid.TryParse(identifier, out var userId)
                    && userId != Guid.Empty
                        ? userId
                        : null;
            }
        }

        public async Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(permission);
            cancellationToken.ThrowIfCancellationRequested();

            var context = _httpContextAccessor.HttpContext;

            if (context is null || UserId is null)
            {
                return false;
            }

            var result = await _authorization.AuthorizeAsync(
                context.User,
                resource: null,
                policyName: permission);

            cancellationToken.ThrowIfCancellationRequested();

            return result.Succeeded;
        }
    }
}
