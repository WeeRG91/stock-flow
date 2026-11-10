using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace StockFlow.API.Tests.Fakes
{
    public sealed class TestAuthenticationHandler
        : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "StockFlowTest";

        private readonly TestUserIdentity _user;

        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            TestUserIdentity user)
            : base(options, logger, encoder)
        {
            _user = user;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!_user.IsAuthenticated)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, _user.Id.ToString()),
                new(ClaimTypes.Email, _user.Email),
                new(ClaimTypes.Name, _user.DisplayName)
            };

            claims.AddRange(
                _user.Roles.Select(role =>
                    new Claim(ClaimTypes.Role, role)));

            var identity = new ClaimsIdentity(
                claims,
                Scheme.Name);

            var principal = new ClaimsPrincipal(identity);

            var ticket = new AuthenticationTicket(
                principal,
                Scheme.Name);

            return Task.FromResult(
                AuthenticateResult.Success(ticket));
        }
    }
}