using Microsoft.AspNetCore.Identity;

namespace StockFlow.Infrastructure.Identity
{
    public sealed class ApplicationUser : IdentityUser<Guid>
    {
        public required string DisplayName { get; set; }

        public ApplicationUser()
        {
            Id = Guid.NewGuid();
        }
    }
}
