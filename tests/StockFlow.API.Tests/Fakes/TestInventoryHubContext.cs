using Microsoft.AspNetCore.SignalR;
using StockFlow.API.Hubs;

namespace StockFlow.API.Tests.Fakes
{
    public sealed class TestInventoryHubContext : IHubContext<InventoryHub>
    {
        public TestInventoryHubContext(IClientProxy client)
        {
            Clients = new BroadcastClients(client);
        }

        public IHubClients Clients { get; }
        public IGroupManager Groups => throw new NotSupportedException();

        private sealed class BroadcastClients(IClientProxy client) : IHubClients
        {
            public IClientProxy All => client;
            public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
            public IClientProxy Client(string connectionId) => throw new NotSupportedException();
            public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
            public IClientProxy Group(string groupName) => throw new NotSupportedException();
            public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
            public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
            public IClientProxy User(string userId) => throw new NotSupportedException();
            public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
        }
    }
}
