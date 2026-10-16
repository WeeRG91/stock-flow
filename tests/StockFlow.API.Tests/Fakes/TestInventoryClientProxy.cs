using Microsoft.AspNetCore.SignalR;

namespace StockFlow.API.Tests.Fakes
{
    public sealed class TestInventoryClientProxy : IClientProxy
    {
        public int SendCallCount { get; private set; }
        public string? LastMethod { get; private set; }
        public object?[]? LastArguments { get; private set; }

        public Exception? SendException { get; set; }

        public Task SendCoreAsync(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default)
        {
            SendCallCount++;
            LastMethod = method;
            LastArguments = args;

            if (SendException is not null)
            {
                return Task.FromException(SendException);
            }

            cancellationToken.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }
    }
}