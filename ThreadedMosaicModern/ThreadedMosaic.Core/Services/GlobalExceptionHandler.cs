using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThreadedMosaic.Core.Exceptions;
using ThreadedMosaic.Core.DTOs;

namespace ThreadedMosaic.Core.Services
{
    /// <summary>
    /// Global exception handling service for consistent error processing
    /// </summary>
    public class GlobalExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles exceptions and converts them to appropriate error responses
        /// </summary>
        public async Task<ErrorResponse> HandleExceptionAsync(Exception exception, CancellationToken cancellationToken = default)
        {
            var errorResponse = exception switch
            {
                ValidationException validationEx => HandleValidationException(validationEx),
                ImageProcessingException imageEx => HandleImageProcessingException(imageEx),
                FileOperationException fileEx => HandleFileOperationException(fileEx),
                MosaicCreationException mosaicEx => HandleMosaicCreationException(mosaicEx),
                InsufficientResourcesException resourceEx => HandleInsufficientResourcesException(resourceEx),
                UnsupportedImageFormatException formatEx => HandleUnsupportedImageFormatException(formatEx),
                MosaicCancelledException cancelledEx => HandleMosaicCancelledException(cancelledEx),
                OperationCanceledException => HandleOperationCancelledException(),
                ArgumentException argEx => HandleArgumentException(argEx),
                UnauthorizedAccessException => HandleUnauthorizedAccessException(),
                OutOfMemoryException => HandleOutOfMemoryException(),
                _ => HandleGenericException(exception)
            };

            // Log the exception with appropriate level
            LogException(exception, errorResponse.Severity);

            return await Task.FromResult(errorResponse);
        }

        /// <summary>
        /// Safely executes an operation with exception handling
        /// </summary>
        public async Task<TResult> ExecuteAsync<TResult>(
            Func<Task<TResult>> operation, 
            Func<Exception, Task<TResult>>? errorHandler = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (Exception ex) when (errorHandler != null)
            {
                _logger.LogError(ex, "Operation failed, using error handler");
                return await errorHandler(ex).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Operation failed without error handler");
                throw;
            }
        }

        /// <summary>
        /// Safely executes an operation with exception handling (non-generic version)
        /// </summary>
        public async Task ExecuteAsync(
            Func<Task> operation, 
            Func<Exception, Task>? errorHandler = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await operation().ConfigureAwait(false);
            }
            catch (Exception ex) when (errorHandler != null)
            {
                _logger.LogError(ex, "Operation failed, using error handler");
                await errorHandler(ex).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Operation failed without error handler");
                throw;
            }
        }

        private ErrorResponse HandleValidationException(ValidationException ex)
        {
            return new ErrorResponse
            {
                ErrorCode = "VALIDATION_ERROR",
                Message = "Validation failed",
                Details = ex.ValidationErrors,
                Severity = ErrorSeverity.Warning,
                Timestamp = DateTime.UtcNow
            };
        }

        private ErrorResponse HandleImageProcessingException(ImageProcessingException ex)
        {
            return new ErrorResponse
            {
                ErrorCode = "IMAGE_PROCESSING_ERROR",
                Message = ex.Message,
                Details = new[] { $"Image: {ex.ImagePath ?? "Unknown"}" },
                Severity = ErrorSeverity.Error,
                Timestamp = DateTime.UtcNow
            };
        }

        private ErrorResponse HandleFileOperationException(FileOperationException ex)
        {
            return new ErrorResponse
            {
                ErrorCode = "FILE_OPERATION_ERROR",
                Message = ex.Message,
                Details = new[] { 
                    $"File: {ex.FilePath ?? "Unknown"}", 
                    $"Operation: {ex.Operation ?? "Unknown"}" 
                },
                Severity = ErrorSeverity.Error,
                Timestamp = DateTime.UtcNow
            };
        }

        private ErrorResponse HandleMosaicCreationException(MosaicCreationException ex)
        {
            return new ErrorResponse
            {
                ErrorCode = "MOSAIC_CREATION_ERROR",
                Message = ex.Message,
                Details = new[] { 
                    $"Mosaic ID: {ex.MosaicId?.ToString() ?? "Unknown"}", 
                    $"Type: {ex.MosaicType ?? "Unknown"}" 
                },
                Severity = ErrorSeverity.Error,
                Timestamp = DateTime.UtcNow
            };
        }

        private ErrorResponse HandleInsufficientResourcesException(InsufficientResourcesException ex)
        {
            return new ErrorResponse
            {
                ErrorCode = "INSUFFICIENT_RESOURCES",
                Message = ex.Message,
                Details = new[] { 
                    $"Resource: {ex.ResourceType ?? "Unknown"}", 
                    $"Required: {ex.RequiredAmount?.ToString() ?? "Unknown"}",
                    $"Available: {ex.AvailableAmount?.ToString() ?? "Unknown"}"
                },
                Severity = ErrorSeverity.Warning,
                Timestamp = DateTime.UtcNow
            };
        }

        private ErrorResponse HandleUnsupportedImageFormatException(UnsupportedImageFormatException ex)
        {
            return new ErrorResponse
            {
                ErrorCode = "UNSUPPORTED_FORMAT",
                Message = ex.Message,
                Details = new[] { 
                    $"File: {ex.ImagePath ?? "Unknown"}", 
                    $"Format: {ex.Format ?? "Unknown"}" 
                },
                Severity = ErrorSeverity.Warning,
                Timestamp = DateTime.UtcNow
            };
        }

        private ErrorResponse HandleMosaicCancelledException(MosaicCancelledException ex)
        {
            return new ErrorResponse
            {
                ErrorCode = "OPERATION_CANCELLED",
                Message = ex.Message,
                Details = new[] { $"Mosaic ID: {ex.MosaicId?.ToString() ?? "Unknown"}" },
                Severity = ErrorSeverity.Information,
                Timestamp = DateTime.UtcNow
            };
        }

        private ErrorResponse HandleOperationCancelledException()
        {
            return new ErrorResponse
            {
                ErrorCode = "OPERATION_CANCELLED",
                Message = "The operation was cancelled",
                Details = Array.Empty<string>(),
                Severity = ErrorSeverity.Information,
                Timestamp = DateTime.UtcNow
            };
        }

        private ErrorResponse HandleArgumentException(ArgumentException ex)
        {
            return new ErrorResponse
            {
                ErrorCode = "INVALID_ARGUMENT",
                Message = ex.Message,
                Details = new[] { $"Parameter: {ex.ParamName ?? "Unknown"}" },
                Severity = ErrorSeverity.Warning,
                Timestamp = DateTime.UtcNow
            };
        }

        private ErrorResponse HandleUnauthorizedAccessException()
        {
            return new ErrorResponse
            {
                ErrorCode = "UNAUTHORIZED_ACCESS",
                Message = "Access to the resource is denied",
                Details = Array.Empty<string>(),
                Severity = ErrorSeverity.Error,
                Timestamp = DateTime.UtcNow
            };
        }

        private ErrorResponse HandleOutOfMemoryException()
        {
            return new ErrorResponse
            {
                ErrorCode = "OUT_OF_MEMORY",
                Message = "The system is out of memory. Try processing smaller images or reduce concurrent operations.",
                Details = Array.Empty<string>(),
                Severity = ErrorSeverity.Critical,
                Timestamp = DateTime.UtcNow
            };
        }

        private ErrorResponse HandleGenericException(Exception ex)
        {
            return new ErrorResponse
            {
                ErrorCode = "INTERNAL_ERROR",
                Message = "An unexpected error occurred",
                Details = new[] { ex.GetType().Name },
                Severity = ErrorSeverity.Critical,
                Timestamp = DateTime.UtcNow
            };
        }

        private void LogException(Exception exception, ErrorSeverity severity)
        {
            var logLevel = severity switch
            {
                ErrorSeverity.Information => LogLevel.Information,
                ErrorSeverity.Warning => LogLevel.Warning,
                ErrorSeverity.Error => LogLevel.Error,
                ErrorSeverity.Critical => LogLevel.Critical,
                _ => LogLevel.Error
            };

            _logger.Log(logLevel, exception, "Global exception handler processed: {ExceptionType}", exception.GetType().Name);
        }
    }

    /// <summary>
    /// Standard error response structure
    /// </summary>
    public class ErrorResponse
    {
        public string ErrorCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string[] Details { get; set; } = Array.Empty<string>();
        public ErrorSeverity Severity { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Error severity levels
    /// </summary>
    public enum ErrorSeverity
    {
        Information,
        Warning,
        Error,
        Critical
    }
}