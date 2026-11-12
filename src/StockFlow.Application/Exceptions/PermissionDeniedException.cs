namespace StockFlow.Application.Exceptions
{
    public sealed class PermissionDeniedException : Exception
    {
        public PermissionDeniedException()
            : base("You do not have permission to perform this operation.")
        {
        }
    }
}
