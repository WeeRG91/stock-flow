using StockFlow.Application.Abstractions;

namespace StockFlow.Application.Tests.Fakes;

internal sealed class FakeCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }

    public HashSet<string> Permissions { get; } =
        new(StringComparer.Ordinal);

    public Task<bool> HasPermissionAsync(
        string permission,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Permissions.Contains(permission));
    }
}