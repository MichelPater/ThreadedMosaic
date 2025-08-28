using System.Net;
using System.Text.Json;
using ThreadedMosaic.Core.Exceptions;

namespace ThreadedMosaic.Api.Middleware
{
    /// <summary>
    /// Global exception handling middleware for consistent error responses
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
                _logger.LogError(ex, "An unhandled exception occurred");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            
            var response = new ErrorResponse();

            switch (exception)
            {

                case ArgumentException:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response.Error = "Invalid Request";
                    response.Message = exception.Message;
                    break;

                case UnauthorizedAccessException:
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    response.Error = "Unauthorized";
                    response.Message = "Access to the requested resource is denied";
                    break;

                case FileNotFoundException:
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.Error = "File Not Found";
                    response.Message = "The requested file could not be found";
                    break;

                case DirectoryNotFoundException:
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.Error = "Directory Not Found";
                    response.Message = "The requested directory could not be found";
                    break;

                case OperationCanceledException:
                    response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                    response.Error = "Operation Cancelled";
                    response.Message = "The operation was cancelled";
                    break;

                case TimeoutException:
                    response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                    response.Error = "Timeout";
                    response.Message = "The operation timed out";
                    break;

                default:
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    response.Error = "Internal Server Error";
                    response.Message = "An unexpected error occurred";
                    break;
            }

            context.Response.StatusCode = response.StatusCode;

            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
        }

        private class ErrorResponse
        {
            public int StatusCode { get; set; }
            public string Error { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string? Details { get; set; }
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        }
    }
}