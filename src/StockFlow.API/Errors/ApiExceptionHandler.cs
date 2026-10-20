using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StockFlow.Application.Exceptions;

namespace StockFlow.API.Errors
{
    public sealed class ApiExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<ApiExceptionHandler> _logger;

        public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            if (exception is RequestValidationException validation)
            {
                var validationProblem = new ValidationProblemDetails(
                    new Dictionary<string, string[]>
                    {
                        [validation.FieldName] = new[] { validation.Message }
                    })
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred.",
                    Instance = httpContext.Request.Path.Value
                };

                validationProblem.Extensions["traceId"] =
                    httpContext.TraceIdentifier;

                httpContext.Response.StatusCode =
                    StatusCodes.Status400BadRequest;

                await httpContext.Response.WriteAsJsonAsync(
                    validationProblem,
                    options: null,
                    contentType: "application/problem+json",
                    cancellationToken: cancellationToken);

                return true;
            }

            var (status, title, detail) = exception switch
            {
                ResourceNotFoundException missing => (
                    StatusCodes.Status404NotFound,
                    "Resource not found",
                    missing.Message),

                InventoryConflictException conflict => (
                    StatusCodes.Status409Conflict,
                    "Inventory conflict",
                    conflict.Message),

                BusinessRuleException businessRule => (
                    StatusCodes.Status409Conflict,
                    "Business rule violation",
                    businessRule.Message),

                SupplierCodeConflictException supplierConflict => (
                    StatusCodes.Status409Conflict,
                    "Supplier code conflict",
                    supplierConflict.Message),

                _ => (
                    StatusCodes.Status500InternalServerError,
                    "Internal server error",
                    "An unexpected error occurred.")
            };

            if (status == StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(
                    exception,
                    "Unexpected request failure. TraceId: {TraceId}",
                    httpContext.TraceIdentifier);
            }

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path.Value
            };

            problem.Extensions["traceId"] = httpContext.TraceIdentifier;

            httpContext.Response.StatusCode = status;

            await httpContext.Response.WriteAsJsonAsync(
                problem,
                options: null,
                contentType: "application/problem+json",
                cancellationToken: cancellationToken);

            return true;
        }
    }
}
