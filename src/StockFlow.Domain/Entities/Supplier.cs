using System.Net.Mail;

namespace StockFlow.Domain.Entities
{
    public class Supplier
    {
        public Guid Id { get; }
        public string Code { get; }
        public string Name { get; }
        public string ContactEmail { get; }
        public string? PhoneNumber { get; }
        public bool IsActive { get; private set; }

        public Supplier(
            string code,
            string name,
            string contactEmail,
            string? phoneNumber = null)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException(
                    "Supplier code must not be empty or whitespace.",
                    nameof(code));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Supplier name must not be empty or whitespace.",
                    nameof(name));
            }

            if (string.IsNullOrWhiteSpace(contactEmail))
            {
                throw new ArgumentException(
                    "Contact email must not be empty or whitespace.",
                    nameof(contactEmail));
            }

            var normalizedEmail = contactEmail.Trim();

            if (normalizedEmail.Contains('\r') ||
                normalizedEmail.Contains('\n') ||
                !MailAddress.TryCreate(normalizedEmail, out var parsedEmail) ||
                !string.Equals(
                    parsedEmail.Address,
                    normalizedEmail,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Contact email must be a single valid email address.",
                    nameof(contactEmail));
            }

            Id = Guid.NewGuid();
            Code = code.Trim().ToUpperInvariant();
            Name = name.Trim();
            ContactEmail = normalizedEmail;

            PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber)
                ? null
                : phoneNumber.Trim();

            IsActive = true;
        }

        public void Activate()
        {
            IsActive = true;
        }

        public void Deactivate()
        {
            IsActive = false;
        }
    }
}
