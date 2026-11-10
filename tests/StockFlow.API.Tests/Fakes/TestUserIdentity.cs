using StockFlow.Application.Authorization;

namespace StockFlow.API.Tests.Fakes
{
    public sealed class TestUserIdentity
    {
        public bool IsAuthenticated { get; set; } = true;

        public Guid Id { get; set; } =
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        public string Email { get; set; } = "test-user@example.com";

        public string DisplayName { get; set; } = "Test User";

        public string[] Roles { get; set; } =
        [
            StockFlowRoles.Administrator
        ];
    }
}