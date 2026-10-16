using StockFlow.API.Errors;
using StockFlow.API.Hubs;
using StockFlow.API.Notifications;
using StockFlow.Application.Abstractions.Notifications;
using StockFlow.Application.Inventory.GetInventory;
using StockFlow.Application.Inventory.ReceiveStock;
using StockFlow.Application.Products.GetProducts;
using StockFlow.Application.Warehouses.GetWarehouses;
using StockFlow.Infrastructure;
using StockFlow.Infrastructure.Development;
using StockFlow.Infrastructure.Persistence;

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

builder.Services.AddScoped<IInventoryChangeNotifier, SignalRInventoryChangeNotifier>();

var connectionString = builder.Configuration.GetConnectionString("StockFlow");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Configure ConnectionStrings:StockFlow before starting the API.");
}

builder.Services.AddInfrastructure(connectionString);

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

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.MapHub<InventoryHub>("/hubs/inventory");

app.Run();

public partial class Program
{
}
