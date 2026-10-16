namespace StockFlow.Application.Exceptions
{
    public sealed class RequestValidationException : Exception
    {
        public string FieldName { get; }

        public RequestValidationException(
            string fieldName,
            string message)
            : base(message)
        {
            FieldName = fieldName;
        }
    }
}
