using Microsoft.AspNetCore.Identity;
using StockFlow.Application.Abstractions.Authentication;
using StockFlow.Application.Authentication;

namespace StockFlow.Infrastructure.Identity
{
    public sealed class IdentityAccountService : IAccountService
    {
        private readonly UserManager<ApplicationUser> _users;
        private readonly SignInManager<ApplicationUser> _signIn;

        public IdentityAccountService(
            UserManager<ApplicationUser> users,
            SignInManager<ApplicationUser> signIn)
        {
            ArgumentNullException.ThrowIfNull(users);
            ArgumentNullException.ThrowIfNull(signIn);

            _users = users;
            _signIn = signIn;
        }

        public async Task<UserProfileDto?> GetProfileAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (userId == Guid.Empty)
            {
                throw new ArgumentException(
                    "User ID must not be empty.",
                    nameof(userId));
            }

            var user = await _users.FindByIdAsync(userId.ToString());

            if (user is null)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var roles = await _users.GetRolesAsync(user);

            cancellationToken.ThrowIfCancellationRequested();

            return new UserProfileDto(
                user.Id,
                user.Email
                    ?? throw new InvalidOperationException(
                        "The account email is missing."),
                user.DisplayName,
                roles.OrderBy(role => role, StringComparer.Ordinal)
                    .ToArray());
        }

        public async Task<UserProfileDto?> SignInAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrEmpty(password))
            {
                return null;
            }

            var user = await _users.FindByEmailAsync(email.Trim());

            if (user is null)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var result = await _signIn.CheckPasswordSignInAsync(
                user,
                password,
                lockoutOnFailure: true);

            if (!result.Succeeded || user.TwoFactorEnabled)
            {
                return null;
            }

            var profile = await GetProfileAsync(
                user.Id,
                cancellationToken);

            if (profile is null)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            await _signIn.SignInAsync(
                user,
                isPersistent: false);

            return profile;
        }


        public async Task SignOutAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (userId == Guid.Empty)
            {
                throw new ArgumentException(
                    "User ID must not be empty.",
                    nameof(userId));
            }

            try
            {
                var user = await _users.FindByIdAsync(userId.ToString());

                if (user is not null)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var result = await _users.UpdateSecurityStampAsync(user);

                    if (!result.Succeeded)
                    {
                        throw new InvalidOperationException(
                            "The user's sessions could not be revoked.");
                    }
                }
            }
            finally
            {
                await _signIn.SignOutAsync();
            }
        }
    }
}
