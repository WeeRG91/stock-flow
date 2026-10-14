namespace StockFlow.Domain.Entities
{
    public class Product
    {
        public Guid Id { get; }
        public string Name { get; }
        public string Sku { get; }
        public string? Description { get; }
        public bool IsActive { get; private set; }

        public Product(
            string name,
            string sku,
            string? description = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                   "Product name must not be empty or whitespace.",
                   nameof(name));
            }

            if (string.IsNullOrWhiteSpace(sku))
            {
                throw new ArgumentException(
                   "Product SKU must not be empty or whitespace.",
                   nameof(sku));
            }

            Id = Guid.NewGuid();
            Name = name.Trim();
            Sku = sku.Trim().ToUpperInvariant();
            Description = string.IsNullOrWhiteSpace(description)
                ? null
                : description.Trim();
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
