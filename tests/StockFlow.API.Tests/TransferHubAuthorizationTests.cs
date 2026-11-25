using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using StockFlow.Application.Authorization;

namespace StockFlow.API.Tests;

public sealed class TransferHubAuthorizationTests
{
    [Theory]
    [InlineData(StockFlowRoles.Administrator, HttpStatusCode.OK)]
    [InlineData(StockFlowRoles.InventoryManager, HttpStatusCode.OK)]
    [InlineData(StockFlowRoles.PurchasingOfficer, HttpStatusCode.OK)]
    [InlineData(StockFlowRoles.PurchasingManager, HttpStatusCode.OK)]
    [InlineData(StockFlowRoles.WarehouseStaff, HttpStatusCode.OK)]
    [InlineData(StockFlowRoles.Viewer, HttpStatusCode.OK)]
    [InlineData("UnknownRole", HttpStatusCode.Forbidden)]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    public async Task Negotiate_RequiresTransferReadAccess(string? role, HttpStatusCode expectedStatus)
    {
        using var factory = new StockFlowApiFactory();
        factory.User.IsAuthenticated = role is not null;
        factory.User.Roles = role is null ? [] : [role];
        using var client = await factory.CreateClientWithAntiforgeryAsync();

        using var response = await client.PostAsync(
            "/hubs/transfers/negotiate?negotiateVersion=1", null);

        Assert.Equal(expectedStatus, response.StatusCode);
        if (expectedStatus == HttpStatusCode.OK)
        {
            var negotiation = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(1, negotiation.GetProperty("negotiateVersion").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(
                negotiation.GetProperty("connectionToken").GetString()));
        }
        else
        {
            Assert.Null(response.Headers.Location);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.NotNull(problem);
            Assert.Equal((int)expectedStatus, problem.Status);
        }
    }
}
