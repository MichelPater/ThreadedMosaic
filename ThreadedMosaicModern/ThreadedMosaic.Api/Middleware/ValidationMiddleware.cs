using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace ThreadedMosaic.Api.Middleware
{
    /// <summary>
    /// Middleware for validating file uploads and request parameters
    /// </summary>
    public class ValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ValidationMiddleware> _logger;
        private readonly long _maxRequestSize = 100 * 1024 * 1024; // 100MB
        private readonly string[] _allowedImageTypes = { "image/jpeg", "image/png", "image/bmp", "image/tiff", "image/webp" };

        public ValidationMiddleware(RequestDelegate next, ILogger<ValidationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Validate request size
            if (context.Request.ContentLength.HasValue && context.Request.ContentLength.Value > _maxRequestSize)
            {
                _logger.LogWarning("Request size {Size} exceeds maximum allowed size {MaxSize}", 
                    context.Request.ContentLength.Value, _maxRequestSize);
                
                await WriteErrorResponse(context, 413, "Request Too Large", 
                    $"Request size exceeds maximum allowed size of {_maxRequestSize / 1024 / 1024}MB");
                return;
            }

            // Validate file uploads if present
            if (context.Request.HasFormContentType && context.Request.Form.Files.Any())
            {
                var validationResult = ValidateFiles(context.Request.Form.Files);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("File validation failed: {Error}", validationResult.Error);
                    await WriteErrorResponse(context, 400, "Invalid File", validationResult.Error);
                    return;
                }
            }

            // Validate specific endpoints
            if (context.Request.Path.StartsWithSegments("/api/mosaic"))
            {
                var endpointValidation = await ValidateMosaicEndpoint(context);
                if (!endpointValidation.IsValid)
                {
                    _logger.LogWarning("Endpoint validation failed: {Error}", endpointValidation.Error);
                    await WriteErrorResponse(context, 400, "Invalid Request", endpointValidation.Error);
                    return;
                }
            }

            await _next(context);
        }

        private (bool IsValid, string? Error) ValidateFiles(IFormFileCollection files)
        {
            foreach (var file in files)
            {
                // Check file size
                if (file.Length == 0)
                    return (false, $"File '{file.FileName}' is empty");

                if (file.Length > 50 * 1024 * 1024) // 50MB per file
                    return (false, $"File '{file.FileName}' exceeds maximum size of 50MB");

                // Check file type
                if (!_allowedImageTypes.Contains(file.ContentType?.ToLowerInvariant()))
                    return (false, $"File '{file.FileName}' has unsupported content type: {file.ContentType}");

                // Check file extension
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".webp" };
                if (!allowedExtensions.Contains(extension))
                    return (false, $"File '{file.FileName}' has unsupported extension: {extension}");

                // Basic security check - ensure filename doesn't contain path traversal
                if (file.FileName.Contains("..") || file.FileName.Contains("/") || file.FileName.Contains("\\"))
                    return (false, $"File '{file.FileName}' contains invalid characters");
            }

            return (true, null);
        }

        private async Task<(bool IsValid, string? Error)> ValidateMosaicEndpoint(HttpContext context)
        {
            // Only validate POST requests with JSON content
            if (context.Request.Method != "POST" || !context.Request.ContentType?.StartsWith("application/json") == true)
                return (true, null);

            try
            {
                // Enable buffering to allow multiple reads
                context.Request.EnableBuffering();
                
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                var requestBody = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0; // Reset for next middleware

                if (string.IsNullOrEmpty(requestBody))
                    return (false, "Request body is required");

                // Basic JSON validation
                try
                {
                    using var document = JsonDocument.Parse(requestBody);
                    var root = document.RootElement;

                    // Validate common required fields
                    if (!root.TryGetProperty("masterImagePath", out _))
                        return (false, "masterImagePath is required");

                    if (!root.TryGetProperty("seedImagesDirectory", out _))
                        return (false, "seedImagesDirectory is required");

                    if (!root.TryGetProperty("outputPath", out _))
                        return (false, "outputPath is required");

                    if (!root.TryGetProperty("pixelSize", out var pixelSizeElement))
                        return (false, "pixelSize is required");

                    // Validate pixel size range
                    if (pixelSizeElement.TryGetInt32(out var pixelSize))
                    {
                        if (pixelSize < 4 || pixelSize > 128)
                            return (false, "pixelSize must be between 4 and 128");
                    }

                    // Validate paths don't contain dangerous characters
                    if (root.TryGetProperty("masterImagePath", out var masterPath))
                    {
                        var path = masterPath.GetString();
                        if (string.IsNullOrEmpty(path) || path.Contains(".."))
                            return (false, "Invalid masterImagePath");
                    }

                    if (root.TryGetProperty("outputPath", out var outputPath))
                    {
                        var path = outputPath.GetString();
                        if (string.IsNullOrEmpty(path) || path.Contains(".."))
                            return (false, "Invalid outputPath");
                    }
                }
                catch (JsonException ex)
                {
                    return (false, $"Invalid JSON format: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating mosaic endpoint");
                return (false, "Unable to validate request");
            }

            return (true, null);
        }

        private static async Task WriteErrorResponse(HttpContext context, int statusCode, string error, string message)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var response = new
            {
                statusCode,
                error,
                message,
                timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
    }
}