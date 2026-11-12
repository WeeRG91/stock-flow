using Microsoft.AspNetCore.Authorization;
using StockFlow.Application.Authorization;

namespace StockFlow.API.Authorization
{
    public static class AuthorizationRegistration
    {
        public static IServiceCollection AddStockFlowAuthorization(
            this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();

                var readRoles = new[]
                {
                    StockFlowRoles.Administrator,
                    StockFlowRoles.InventoryManager,
                    StockFlowRoles.PurchasingOfficer,
                    StockFlowRoles.PurchasingManager,
                    StockFlowRoles.WarehouseStaff,
                    StockFlowRoles.Viewer,
                };

                options.AddPolicy(
                    StockFlowPolicies.CatalogRead,
                    policy => policy
                        .RequireAuthenticatedUser()
                        .RequireRole(readRoles));

                options.AddPolicy(
                    StockFlowPolicies.InventoryRead,
                    policy => policy
                        .RequireAuthenticatedUser()
                        .RequireRole(readRoles));

                options.AddPolicy(
                    StockFlowPolicies.SuppliersRead,
                    policy => policy
                        .RequireAuthenticatedUser()
                        .RequireRole(readRoles));

                options.AddPolicy(
                    StockFlowPolicies.InventoryReceive,
                    policy => policy
                        .RequireAuthenticatedUser()
                        .RequireRole(
                            StockFlowRoles.Administrator,
                            StockFlowRoles.InventoryManager,
                            StockFlowRoles.WarehouseStaff));

                options.AddPolicy(
                    StockFlowPolicies.SuppliersManage,
                    policy => policy
                        .RequireAuthenticatedUser()
                        .RequireRole(
                            StockFlowRoles.Administrator,
                            StockFlowRoles.PurchasingOfficer,
                            StockFlowRoles.PurchasingManager));

                options.AddPolicy(
                    StockFlowPolicies.BackgroundJobsManage,
                    policy => policy
                        .RequireAuthenticatedUser()
                        .RequireRole(StockFlowRoles.Administrator));

                options.AddPolicy(
                    StockFlowPolicies.PurchaseOrdersRead,
                    policy => policy
                        .RequireAuthenticatedUser()
                        .RequireRole(readRoles));

                options.AddPolicy(
                    StockFlowPolicies.PurchaseOrdersManage,
                    policy => policy
                    .RequireAuthenticatedUser()
                    .RequireRole(
                        StockFlowRoles.Administrator,
                        StockFlowRoles.PurchasingOfficer,
                        StockFlowRoles.PurchasingManager));

                options.AddPolicy(
                    StockFlowPolicies.PurchaseOrdersApprove,
                    policy => policy
                        .RequireAuthenticatedUser()
                        .RequireRole(
                            StockFlowRoles.Administrator,
                            StockFlowRoles.PurchasingManager));
            });

            return services;
        }
    }
}
