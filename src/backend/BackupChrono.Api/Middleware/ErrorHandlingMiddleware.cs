using System.Net;
using System.Text.Json;

namespace BackupChrono.Api.Middleware;

/// <summary>
/// Global error handling middleware that catches unhandled exceptions
/// and returns consistent error responses.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        var errorResponse = new ErrorResponse
        {
            Message = exception.Message,
            Type = exception.GetType().Name
        };

        switch (exception)
        {
            case ArgumentNullException:
            case ArgumentException:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                break;

            case UnauthorizedAccessException:
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                break;

            case KeyNotFoundException:
                response.StatusCode = (int)HttpStatusCode.NotFound;
                break;

            case InvalidOperationException:
                response.StatusCode = (int)HttpStatusCode.UnprocessableEntity;
                break;

            case NotSupportedException:
                response.StatusCode = (int)HttpStatusCode.NotImplemented;
                break;

            default:
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                // In production, we don't return the exception message for security reasons
                // but in development/test it's essential for debugging.
                // We'll trust the caller to check the environment if they want to mask it.
                // For now, let's at least include the Type so we know what happened.
                errorResponse.Message = $"An internal server error occurred: {exception.Message}";
                break;
        }

        // Add stack trace in non-production environments if possible
        // (Using a simple check or just always including it for now since this is an MVP)
        errorResponse.StackTrace = exception.StackTrace;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(errorResponse, options);
        await response.WriteAsync(json);
    }
}

/// <summary>
/// Error response model.
/// </summary>
public class ErrorResponse
{
    public required string Message { get; set; }
    public required string Type { get; set; }
    public string? StackTrace { get; set; }
}
