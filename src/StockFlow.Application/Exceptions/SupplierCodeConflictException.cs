namespace StockFlow.Application.Exceptions
{
    public sealed class SupplierCodeConflictException : Exception
    {
        public string Code { get; }

        public SupplierCodeConflictException(string code)
            : base($"A supplier with code '{code}' already exists.")
        {
            Code = code;
        }
    }
}
