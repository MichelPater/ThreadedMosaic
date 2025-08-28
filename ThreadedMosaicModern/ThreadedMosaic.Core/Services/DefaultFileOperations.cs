using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThreadedMosaic.Core.Interfaces;

namespace ThreadedMosaic.Core.Services
{
    /// <summary>
    /// Default implementation of IFileOperations (equivalent to original DefaultFileOperations)
    /// Provides a simpler implementation compared to ModernFileOperations for basic scenarios
    /// </summary>
    public class DefaultFileOperations : IFileOperations
    {
        private readonly ILogger<DefaultFileOperations> _logger;
        
        /// <summary>
        /// Singleton instance for backward compatibility with original design
        /// </summary>
        public static readonly DefaultFileOperations Instance = new(new NullLogger<DefaultFileOperations>());

        public DefaultFileOperations(ILogger<DefaultFileOperations> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task OpenImageFileAsync(string imageLocation, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(imageLocation))
                throw new ArgumentException("Image location cannot be null or empty", nameof(imageLocation));

            try
            {
                _logger.LogDebug("Opening image file: {ImageLocation}", imageLocation);
                
                // Use the modern file operations logic for opening files
                var modernOperations = new ModernFileOperations(_logger as ILogger<ModernFileOperations> ?? new NullLogger<ModernFileOperations>());
                await modernOperations.OpenImageFileAsync(imageLocation, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open image file: {ImageLocation}", imageLocation);
                throw;
            }
        }

        public async Task<bool> IsValidImageFileAsync(string filePath)
        {
            var modernOperations = new ModernFileOperations(_logger as ILogger<ModernFileOperations> ?? new NullLogger<ModernFileOperations>());
            return await modernOperations.IsValidImageFileAsync(filePath).ConfigureAwait(false);
        }

        public async Task<long> GetFileSizeAsync(string filePath)
        {
            var modernOperations = new ModernFileOperations(_logger as ILogger<ModernFileOperations> ?? new NullLogger<ModernFileOperations>());
            return await modernOperations.GetFileSizeAsync(filePath).ConfigureAwait(false);
        }

        public async Task CopyFileAsync(string sourcePath, string destinationPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var modernOperations = new ModernFileOperations(_logger as ILogger<ModernFileOperations> ?? new NullLogger<ModernFileOperations>());
            await modernOperations.CopyFileAsync(sourcePath, destinationPath, progress, cancellationToken).ConfigureAwait(false);
        }

        public async Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var modernOperations = new ModernFileOperations(_logger as ILogger<ModernFileOperations> ?? new NullLogger<ModernFileOperations>());
            await modernOperations.DeleteFileAsync(filePath, cancellationToken).ConfigureAwait(false);
        }

        public async Task<long> GetAvailableDiskSpaceAsync(string path)
        {
            var modernOperations = new ModernFileOperations(_logger as ILogger<ModernFileOperations> ?? new NullLogger<ModernFileOperations>());
            return await modernOperations.GetAvailableDiskSpaceAsync(path).ConfigureAwait(false);
        }

        public async Task<string> CreateTempDirectoryAsync()
        {
            var modernOperations = new ModernFileOperations(_logger as ILogger<ModernFileOperations> ?? new NullLogger<ModernFileOperations>());
            return await modernOperations.CreateTempDirectoryAsync().ConfigureAwait(false);
        }

        public async Task CleanupTempFilesAsync(string tempPath, CancellationToken cancellationToken = default)
        {
            var modernOperations = new ModernFileOperations(_logger as ILogger<ModernFileOperations> ?? new NullLogger<ModernFileOperations>());
            await modernOperations.CleanupTempFilesAsync(tempPath, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Null logger implementation for fallback scenarios
    /// </summary>
    internal class NullLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}