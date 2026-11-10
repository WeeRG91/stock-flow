using StockFlow.Application.Abstractions.Authentication;
using StockFlow.Application.Authentication;

namespace StockFlow.API.Tests.Fakes
{
    public sealed class TestAccountService : IAccountService
    {
        public UserProfileDto? Profile { get; set; }

        public Task<UserProfileDto?> GetProfileAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var profile = Profile;

            return Task.FromResult(
                profile is not null && profile.Id == userId
                    ? profile
                    : null);
        }

        public Task<UserProfileDto?> SignInAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "This fake supports current-user profile tests only.");
        }

        public Task SignOutAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "This fake supports current-user profile tests only.");
        }
    }
}