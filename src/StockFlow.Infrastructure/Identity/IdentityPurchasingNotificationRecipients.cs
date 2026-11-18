using Microsoft.AspNetCore.Identity;
using StockFlow.Application.Abstractions.Notifications;
using StockFlow.Application.Authorization;

namespace StockFlow.Infrastructure.Identity
{
    public sealed class IdentityPurchasingNotificationRecipients : IPurchasingNotificationRecipients
    {
        private readonly UserManager<ApplicationUser> _users;

        public IdentityPurchasingNotificationRecipients(
            UserManager<ApplicationUser> users)
        {
            ArgumentNullException.ThrowIfNull(users);
            _users = users;
        }

        public async Task<IReadOnlyList<Guid>> GetApproverUserIdsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var administrators = await _users.GetUsersInRoleAsync(
                StockFlowRoles.Administrator);

            cancellationToken.ThrowIfCancellationRequested();

            var purchasingManagers = await _users.GetUsersInRoleAsync(
                StockFlowRoles.PurchasingManager);

            cancellationToken.ThrowIfCancellationRequested();

            return administrators
                .Concat(purchasingManagers)
                .Select(user => user.Id)
                .Distinct()
                .ToArray();
        }
    }
}
