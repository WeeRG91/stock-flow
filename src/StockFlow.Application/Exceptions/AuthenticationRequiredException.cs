namespace StockFlow.Application.Exceptions
{
    public sealed class AuthenticationRequiredException : Exception
    {
        public AuthenticationRequiredException()
            : base("Authentication is required.")
        {
        }
    }
}
