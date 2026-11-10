using StockFlow.Application.Authentication;

namespace StockFlow.Application.Abstractions.Authentication
{
    public interface IAccountService
    {
        /// <summary>
        /// Establishes a session when credentials are accepted.
        /// Returns null when sign-in is rejected.
        /// </summary>
        Task<UserProfileDto?> SignInAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the user's profile, or null if the user no longer exists.
        /// </summary>
        Task<UserProfileDto?> GetProfileAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Revokes the user's sessions and signs out the current session.
        /// </summary>
        Task SignOutAsync(
            Guid userId,
            CancellationToken cancellationToken = default);
    }
}
