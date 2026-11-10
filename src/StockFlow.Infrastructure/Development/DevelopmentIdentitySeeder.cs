using Microsoft.AspNetCore.Identity;
using StockFlow.Application.Authorization;
using StockFlow.Infrastructure.Identity;

namespace StockFlow.Infrastructure.Development
{
    public static class DevelopmentIdentitySeeder
    {
        public static async Task SeedRolesAsync(
            RoleManager<IdentityRole<Guid>> roles,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(roles);

            var roleNames = new[]
            {
                StockFlowRoles.Administrator,
                StockFlowRoles.InventoryManager,
                StockFlowRoles.PurchasingOfficer,
                StockFlowRoles.PurchasingManager,
                StockFlowRoles.WarehouseStaff,
                StockFlowRoles.Viewer,
            };

            foreach (var roleName in roleNames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (await roles.RoleExistsAsync(roleName))
                {
                    continue;
                }

                var role = new IdentityRole<Guid>(roleName)
                {
                    Id = Guid.NewGuid()
                };

                var result = await roles.CreateAsync(role);

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Development role provisioning failed.");
                }
            }
        }

        public static async Task SeedUsersAsync(
            UserManager<ApplicationUser> users,
            string password,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(users);
            ArgumentException.ThrowIfNullOrWhiteSpace(password);

            (string Email, string DisplayName, string Role)[] accounts =
            {
                (
                    "admin@stockflow.example",
                    "[DEV] Administrator",
                    StockFlowRoles.Administrator
                ),
                (
                    "inventory.manager@stockflow.example",
                    "[DEV] Inventory Manager",
                    StockFlowRoles.InventoryManager
                ),
                (
                    "purchasing.officer@stockflow.example",
                    "[DEV] Purchasing Officer",
                    StockFlowRoles.PurchasingOfficer
                ),
                (
                    "purchasing.manager@stockflow.example",
                    "[DEV] Purchasing Manager",
                    StockFlowRoles.PurchasingManager
                ),
                (
                    "warehouse.staff@stockflow.example",
                    "[DEV] Warehouse Staff",
                    StockFlowRoles.WarehouseStaff
                ),
                (
                    "viewer@stockflow.example",
                    "[DEV] Viewer",
                    StockFlowRoles.Viewer
                )
            };

            foreach (var account in accounts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var user = await users.FindByEmailAsync(account.Email);

                if (user is null)
                {
                    user = new ApplicationUser
                    {
                        UserName = account.Email,
                        Email = account.Email,
                        DisplayName = account.DisplayName,
                        LockoutEnabled = true
                    };

                    var creation = await users.CreateAsync(user, password);

                    if (!creation.Succeeded)
                    {
                        throw new InvalidOperationException(
                            "Development user provisioning failed: " +
                            string.Join(", ",
                                creation.Errors.Select(error => error.Code)));
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!await users.IsInRoleAsync(user, account.Role))
                {
                    var assignment = await users.AddToRoleAsync(
                        user,
                        account.Role);

                    if (!assignment.Succeeded)
                    {
                        throw new InvalidOperationException(
                            "Development role assignment failed.");
                    }
                }
            }
        }
    }
}
