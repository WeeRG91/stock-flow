namespace StockFlow.Application.Exceptions
{
    public sealed class ResourceNotFoundException : Exception
    {
        public string ResourceName { get; }
        public Guid ResourceId { get; }

        public ResourceNotFoundException(
            string resourceName,
            Guid resourceId)
            : base($"{resourceName} was not found.")
        {
            ResourceName = resourceName;
            ResourceId = resourceId;
        }
    }
}
