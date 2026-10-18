using StockFlow.API.Errors;
using StockFlow.API.Hubs;
using StockFlow.API.Notifications;
using StockFlow.Application.Abstractions.Notifications;
using StockFlow.Application.Inventory.GetInventory;
using StockFlow.Application.Inventory.ReceiveStock;
using StockFlow.Application.Products.GetProducts;
using StockFlow.Application.Warehouses.GetWarehouses;
using StockFlow.Infrastructure;
using StockFlow.Infrastructure.BackgroundJobs;
using StockFlow.Infrastructure.Development;
using StockFlow.Infrastructure.Persistence;
using Hangfire;
using StockFlow.Application.Inventory.MonitorLowStock;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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

builder.Services.AddScoped<IInventoryChangeNotifier, SignalRInventoryChangeNotifier>();

var connectionString = builder.Configuration.GetConnectionString("StockFlow");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Configure ConnectionStrings:StockFlow before starting the API.");
}

builder.Services.AddInfrastructure(connectionString);

var configureDemoMonitoring =
    builder.Configuration.GetValue<bool>("ConfigureDemoMonitoring");

var runBackgroundJobs =
    builder.Environment.IsDevelopment() &&
    builder.Configuration.GetValue<bool>("BackgroundJobs:Enabled", true) &&
    !builder.Configuration.GetValue<bool>("SeedDevelopmentData") &&
    !configureDemoMonitoring;

if (runBackgroundJobs)
{
    builder.Services.AddStockFlowBackgroundJobs(connectionString);

    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = 2;
    });
}

var app = builder.Build();

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

if (runBackgroundJobs)
{
    app.MapHangfireDashboard("/hangfire");

    var recurringJobs = app.Services
        .GetRequiredService<IRecurringJobManager>();

    BackgroundJobScheduling.RegisterRecurringJobs(recurringJobs);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.MapHub<InventoryHub>("/hubs/inventory");

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
