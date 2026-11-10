namespace StockFlow.API.Contracts.Authentication
{
    public sealed record CurrentUserResponse(
        Guid Id,
        string Email,
        string DisplayName,
        IReadOnlyList<string> Roles,
        IReadOnlyList<string> Capabilities);
}