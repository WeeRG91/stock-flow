namespace StockFlow.Application.Abstractions
{
    public interface ICurrentUser
    {
        Guid? UserId { get; }

        Task<bool> HasPermissionAsync(
            string permission,
            CancellationToken cancellationToken = default);
    }
}
