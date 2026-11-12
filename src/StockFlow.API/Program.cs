using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StockFlow.API.Antiforgery;
using StockFlow.API.Authorization;
using StockFlow.API.Errors;
using StockFlow.API.Hubs;
using StockFlow.API.Notifications;
using StockFlow.API.RateLimiting;
using StockFlow.Application.Abstractions;
using StockFlow.Application.Abstractions.Notifications;
using StockFlow.Application.Authorization;
using StockFlow.Application.Inventory.GetInventory;
using StockFlow.Application.Inventory.MonitorLowStock;
using StockFlow.Application.Inventory.ReceiveStock;
using StockFlow.Application.Products.GetProducts;
using StockFlow.Application.PurchaseOrders.ApprovePurchaseOrder;
using StockFlow.Application.PurchaseOrders.CreatePurchaseOrder;
using StockFlow.Application.PurchaseOrders.GetPurchaseOrderById;
using StockFlow.Application.PurchaseOrders.GetPurchaseOrders;
using StockFlow.Application.PurchaseOrders.RejectPurchaseOrder;
using StockFlow.Application.PurchaseOrders.SubmitPurchaseOrder;
using StockFlow.Application.PurchaseOrders.UpdateDraftPurchaseOrder;
using StockFlow.Application.Suppliers.ActivateSupplier;
using StockFlow.Application.Suppliers.CreateSupplier;
using StockFlow.Application.Suppliers.DeactivateSupplier;
using StockFlow.Application.Suppliers.GetSupplierById;
using StockFlow.Application.Suppliers.GetSuppliers;
using StockFlow.Application.Suppliers.UpdateSupplier;
using StockFlow.Application.Warehouses.GetWarehouses;
using StockFlow.Infrastructure;
using StockFlow.Infrastructure.BackgroundJobs;
using StockFlow.Infrastructure.Development;
using StockFlow.Infrastructure.Identity;
using StockFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddSignalR();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddScoped<ReceiveStockCommandHandler>();
builder.Services.AddScoped<GetProductsQueryHandler>();
builder.Services.AddScoped<GetWarehousesQueryHandler>();
builder.Services.AddScoped<GetInventoryQueryHandler>();
builder.Services.AddScoped<GetLowStockInventoriesQueryHandler>();
builder.Services.AddScoped<MonitorLowStockCommandHandler>();
builder.Services.AddScoped<CreateSupplierCommandHandler>();
builder.Services.AddScoped<GetSuppliersQueryHandler>();
builder.Services.AddScoped<GetSupplierByIdQueryHandler>();
builder.Services.AddScoped<UpdateSupplierCommandHandler>();
builder.Services.AddScoped<ActivateSupplierCommandHandler>();
builder.Services.AddScoped<DeactivateSupplierCommandHandler>();
builder.Services.AddScoped<CreatePurchaseOrderCommandHandler>();
builder.Services.AddScoped<GetPurchaseOrderByIdQueryHandler>();
builder.Services.AddScoped<GetPurchaseOrdersQueryHandler>();
builder.Services.AddScoped<UpdateDraftPurchaseOrderCommandHandler>();
builder.Services.AddScoped<SubmitPurchaseOrderCommandHandler>();
builder.Services.AddScoped<ApprovePurchaseOrderCommandHandler>();
builder.Services.AddScoped<RejectPurchaseOrderCommandHandler>();

builder.Services.AddScoped<IInventoryChangeNotifier, SignalRInventoryChangeNotifier>();

var connectionString = builder.Configuration.GetConnectionString("StockFlow");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Configure ConnectionStrings:StockFlow before starting the API.");
}

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddStockFlowAuthentication();
builder.Services.AddStockFlowAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddStockFlowAntiforgery();
builder.Services.AddStockFlowRateLimiting();

var configureDemoMonitoring =
    builder.Configuration.GetValue<bool>("ConfigureDemoMonitoring");

var provisionDevelopmentIdentity = builder.Configuration.GetValue<bool>("ProvisionDevelopmentIdentity");

if (provisionDevelopmentIdentity &&
    !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "Development identity provisioning requires Development.");
}

var runBackgroundJobs =
    builder.Environment.IsDevelopment() &&
    builder.Configuration.GetValue<bool>("BackgroundJobs:Enabled", true) &&
    !builder.Configuration.GetValue<bool>("SeedDevelopmentData") &&
    !configureDemoMonitoring &&
    !provisionDevelopmentIdentity;

if (runBackgroundJobs)
{
    builder.Services.AddStockFlowBackgroundJobs(connectionString);

    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = 2;
    });
}

var app = builder.Build();

if (provisionDevelopmentIdentity)
{
    var password = builder.Configuration["DevelopmentIdentity:Password"];

    if (string.IsNullOrWhiteSpace(password))
    {
        throw new InvalidOperationException(
            "Configure DevelopmentIdentity:Password before provisioning.");
    }

    await using (var scope = app.Services.CreateAsyncScope())
    {
        var roles = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        await DevelopmentIdentitySeeder.SeedRolesAsync(roles);
        await DevelopmentIdentitySeeder.SeedUsersAsync(users, password);
    }

    app.Logger.LogInformation(
        "Development identity provisioning completed.");

    await app.DisposeAsync();
    return;
}

if (builder.Configuration.GetValue<bool>("SeedDevelopmentData"))
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Development data can only be seeded in Development.");
    }

    await using (var scope = app.Services.CreateAsyncScope())
    {
        var context = scope.ServiceProvider
            .GetRequiredService<StockFlowDbContext>();

        await DevelopmentDataSeeder.SeedAsync(context);
    }

    app.Logger.LogInformation(
        "Synthetic development catalog setup completed.");

    await app.DisposeAsync();
    return;
}

if (configureDemoMonitoring)
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Demo monitoring setup requires Development.");
    }

    await using (var scope = app.Services.CreateAsyncScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<StockFlowDbContext>();

        var inventory = await DevelopmentMonitoringSetup.ConfigureAsync(context);

        app.Logger.LogInformation(
            "Demo monitoring configured. Inventory: {InventoryId}; " +
            "Product: {ProductId}; Warehouse: {WarehouseId}; " +
            "Quantity: {Quantity}; Threshold: {Threshold}.",
            inventory.Id,
            inventory.ProductId,
            inventory.WarehouseId,
            inventory.QuantityOnHand,
            inventory.ReorderThreshold);
    }

    await app.DisposeAsync();
    return;
}

app.UseExceptionHandler();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (runBackgroundJobs)
{
    app.MapHangfireDashboard("/hangfire")
        .RequireAuthorization(StockFlowPolicies.BackgroundJobsManage);

    var recurringJobs = app.Services
        .GetRequiredService<IRecurringJobManager>();

    BackgroundJobScheduling.RegisterRecurringJobs(recurringJobs);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.MapHub<InventoryHub>(
    "/hubs/inventory",
    options =>
    {
        options.CloseOnAuthenticationExpiration = true;
    })
    .RequireAuthorization(StockFlowPolicies.InventoryRead);

if (builder.Configuration.GetValue<bool>("RunDemoJob"))
{
    if (!runBackgroundJobs)
    {
        throw new InvalidOperationException(
            "The demonstration job requires development background processing.");
    }

    var jobClient = app.Services
        .GetRequiredService<IBackgroundJobClient>();

    var jobId = jobClient.Enqueue<DevelopmentDemoJob>(
        job => job.ExecuteAsync(CancellationToken.None));

    app.Logger.LogInformation(
        "Development demonstration job enqueued with ID {JobId}.",
        jobId);
}

app.Run();

public partial class Program
{
}
