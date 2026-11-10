namespace StockFlow.Application.Authentication
{
    public sealed record UserProfileDto(
        Guid Id,
        string Email,
        string DisplayName,
        IReadOnlyList<string> Roles);
}
