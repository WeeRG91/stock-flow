using System.Net.Mail;

namespace StockFlow.Domain.Entities
{
    public class Supplier
    {
        public const int MaxCodeLength = 64;
        public const int MaxNameLength = 200;
        public const int MaxContactEmailLength = 254;
        public const int MaxPhoneNumberLength = 50;

        public Guid Id { get; }
        public string Code { get; }
        public string Name { get; private set; }
        public string ContactEmail { get; private set; }
        public string? PhoneNumber { get; private set; }
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

            var normalizedCode = code.Trim().ToUpperInvariant();

            ValidateMaxLength(normalizedCode, MaxCodeLength, nameof(code));

            var details = NormalizeDetails(name, contactEmail, phoneNumber);

            Id = Guid.NewGuid();
            Code = normalizedCode;
            Name = details.Name;
            ContactEmail = details.ContactEmail;
            PhoneNumber = details.PhoneNumber;
            IsActive = true;
        }

        public void UpdateDetails(
            string name,
            string contactEmail,
            string? phoneNumber = null)
        {
            var details = NormalizeDetails(name, contactEmail, phoneNumber);

            Name = details.Name;
            ContactEmail = details.ContactEmail;
            PhoneNumber = details.PhoneNumber;
        }

        public void Activate()
        {
            IsActive = true;
        }

        public void Deactivate()
        {
            IsActive = false;
        }

        private static (
            string Name,
            string ContactEmail,
            string? PhoneNumber) NormalizeDetails(
                string name,
                string contactEmail,
                string? phoneNumber)
        {
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

            var normalizedName = name.Trim();
            var normalizedEmail = contactEmail.Trim();
            var normalizedPhone = string.IsNullOrWhiteSpace(phoneNumber)
                ? null
                : phoneNumber.Trim();

            ValidateMaxLength(normalizedName, MaxNameLength, nameof(name));
            ValidateMaxLength(
                normalizedEmail, MaxContactEmailLength, nameof(contactEmail));

            if (normalizedPhone is not null)
            {
                ValidateMaxLength(
                    normalizedPhone, MaxPhoneNumberLength, nameof(phoneNumber));
            }

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

            return (normalizedName, normalizedEmail, normalizedPhone);
        }

        private static void ValidateMaxLength(
            string value,
            int maximumLength,
            string parameterName)
        {
            if (value.Length > maximumLength)
            {
                throw new ArgumentException(
                    $"Value must not exceed {maximumLength} characters.",
                    parameterName);
            }
        }
    }
}
