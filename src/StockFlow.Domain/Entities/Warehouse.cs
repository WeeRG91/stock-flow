namespace StockFlow.Domain.Entities
{
    public class Warehouse
    {
        public Guid Id { get; }
        public string Code { get; }
        public string Name { get; }
        public string Location { get; }
        public bool IsActive { get; private set; }

        public Warehouse(
            string code,
            string name,
            string location)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException(
                   "Warehouse code must not be empty or whitespace.",
                   nameof(code));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                   "Warehouse name must not be empty or whitespace.",
                   nameof(name));
            }

            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException(
                   "Warehouse location must not be empty or whitespace.",
                   nameof(location));
            }
            
            Id = Guid.NewGuid();
            Code = code.Trim().ToUpperInvariant();
            Name = name.Trim();
            Location = location.Trim();
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
