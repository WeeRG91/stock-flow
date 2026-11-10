using System.ComponentModel.DataAnnotations;

namespace StockFlow.API.Contracts.Authentication
{
    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string? Email { get; init; }

        [Required]
        public string? Password { get; init; }
    }
}
