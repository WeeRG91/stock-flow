using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.Application.Abstractions.Purchasing;
using StockFlow.Domain.Entities;
using StockFlow.Infrastructure.Purchasing;

namespace StockFlow.Infrastructure.Tests;

public class PurchaseOrderNumberGeneratorTests
{
    [Fact]
    public void Generate_ReturnsNumberAcceptedByPurchaseOrder()
    {
        var generator = new GuidPurchaseOrderNumberGenerator();

        var number = generator.Generate();

        var order = new PurchaseOrder(
            number,
            Guid.NewGuid(),
            "SUP-001",
            "Supplier",
            Guid.NewGuid(),
            "BRU-01",
            "Warehouse",
            new DateTimeOffset(2026, 11, 10, 10, 0, 0, TimeSpan.Zero),
            Guid.NewGuid());

        Assert.StartsWith("PO-", number);
        Assert.True(number.Length <= PurchaseOrder.MaxOrderNumberLength);
        Assert.Equal(number, order.OrderNumber);
    }

    [Fact]
    public void AddInfrastructure_RegistersGeneratorAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddAuthentication().AddIdentityCookies();

        services.AddInfrastructure(
            "Host=localhost;Database=unused_generator_test");

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });

        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider
            .GetRequiredService<IPurchaseOrderNumberGenerator>();

        var second = secondScope.ServiceProvider
            .GetRequiredService<IPurchaseOrderNumberGenerator>();

        Assert.IsType<GuidPurchaseOrderNumberGenerator>(first);
        Assert.Same(first, second);
    }
}