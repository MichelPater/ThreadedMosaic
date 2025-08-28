using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThreadedMosaic.Core.Interfaces;

namespace ThreadedMosaic.Core.Services
{
    /// <summary>
    /// Modern implementation of file operations with async support and better error handling
    /// </summary>
    public class ModernFileOperations : IFileOperations
    {
        private readonly ILogger<ModernFileOperations> _logger;
        private readonly SemaphoreSlim _fileSemaphore;

        public ModernFileOperations(ILogger<ModernFileOperations> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileSemaphore = new SemaphoreSlim(10, 10); // Limit concurrent file operations
        }

        public async Task OpenImageFileAsync(string imageLocation, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(imageLocation))
                throw new ArgumentException("Image location cannot be null or empty", nameof(imageLocation));

            if (!await IsValidImageFileAsync(imageLocation))
                throw new FileNotFoundException($"Image file not found or invalid: {imageLocation}");

            try
            {
                _logger.LogDebug("Opening image file: {ImageLocation}", imageLocation);

                // Use platform-specific methods to open the file
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = imageLocation,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", $"\"{imageLocation}\"");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", $"\"{imageLocation}\"");
                }

                await Task.Delay(100, cancellationToken).ConfigureAwait(false); // Brief delay to ensure process starts
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open image file: {ImageLocation}", imageLocation);
                throw;
            }
        }

        public async Task<bool> IsValidImageFileAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            try
            {
                await _fileSemaphore.WaitAsync().ConfigureAwait(false);
                
                if (!File.Exists(filePath))
                    return false;

                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                var supportedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif", ".webp" };

                if (!Array.Exists(supportedExtensions, ext => ext == extension))
                    return false;

                // Additional check: try to read first few bytes to validate file header
                const int headerSize = 16;
                var buffer = new byte[headerSize];

                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var bytesRead = await fileStream.ReadAsync(buffer, 0, headerSize).ConfigureAwait(false);

                if (bytesRead < 4)
                    return false;

                // Basic file signature validation
                return IsValidImageSignature(buffer, extension);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error validating image file: {FilePath}", filePath);
                return false;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        public async Task<long> GetFileSizeAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                var fileInfo = new FileInfo(filePath);
                return fileInfo.Exists ? fileInfo.Length : 0;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        public async Task CopyFileAsync(string sourcePath, string destinationPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("Source path cannot be null or empty", nameof(sourcePath));
            if (string.IsNullOrWhiteSpace(destinationPath))
                throw new ArgumentException("Destination path cannot be null or empty", nameof(destinationPath));

            if (!File.Exists(sourcePath))
                throw new FileNotFoundException($"Source file not found: {sourcePath}");

            await _fileSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _logger.LogDebug("Copying file: {Source} -> {Destination}", sourcePath, destinationPath);

                // Ensure destination directory exists
                var destinationDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                const int bufferSize = 64 * 1024; // 64KB buffer
                var buffer = new byte[bufferSize];
                var fileSize = new FileInfo(sourcePath).Length;
                long totalBytesRead = 0;

                using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, true);
                using var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, true);

                int bytesRead;
                while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                    
                    totalBytesRead += bytesRead;
                    
                    if (progress != null && fileSize > 0)
                    {
                        var progressPercentage = (double)totalBytesRead / fileSize * 100;
                        progress.Report(progressPercentage);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }

                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Successfully copied file: {Source} -> {Destination} ({Size} bytes)", sourcePath, destinationPath, totalBytesRead);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to copy file: {Source} -> {Destination}", sourcePath, destinationPath);
                
                // Clean up partial destination file if it exists
                try
                {
                    if (File.Exists(destinationPath))
                        File.Delete(destinationPath);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to clean up partial destination file: {Destination}", destinationPath);
                }
                
                throw;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        public async Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            if (!File.Exists(filePath))
            {
                _logger.LogDebug("File does not exist, nothing to delete: {FilePath}", filePath);
                return;
            }

            await _fileSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _logger.LogDebug("Deleting file: {FilePath}", filePath);
                
                // Make file writable if it's read-only
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.IsReadOnly)
                {
                    fileInfo.IsReadOnly = false;
                }

                File.Delete(filePath);
                _logger.LogDebug("Successfully deleted file: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete file: {FilePath}", filePath);
                throw;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        public async Task<long> GetAvailableDiskSpaceAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));

            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                var directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory))
                    directory = Environment.CurrentDirectory;

                var driveInfo = new DriveInfo(Path.GetPathRoot(directory) ?? directory);
                return driveInfo.AvailableFreeSpace;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get available disk space for path: {Path}", path);
                return -1;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        public async Task<string> CreateTempDirectoryAsync()
        {
            await _fileSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), "ThreadedMosaic", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempPath);
                
                _logger.LogDebug("Created temporary directory: {TempPath}", tempPath);
                return tempPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create temporary directory");
                throw;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        public async Task CleanupTempFilesAsync(string tempPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tempPath) || !Directory.Exists(tempPath))
            {
                _logger.LogDebug("Temporary path does not exist or is empty: {TempPath}", tempPath);
                return;
            }

            await _fileSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _logger.LogDebug("Cleaning up temporary files: {TempPath}", tempPath);
                
                // Delete all files first
                var files = Directory.GetFiles(tempPath, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.IsReadOnly)
                            fileInfo.IsReadOnly = false;
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temp file: {File}", file);
                    }
                }

                // Then delete directories
                Directory.Delete(tempPath, true);
                _logger.LogDebug("Successfully cleaned up temporary directory: {TempPath}", tempPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup temporary files: {TempPath}", tempPath);
                // Don't throw - cleanup failures shouldn't break the application
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        private static bool IsValidImageSignature(byte[] bytes, string extension)
        {
            if (bytes.Length < 4)
                return false;

            return extension switch
            {
                ".jpg" or ".jpeg" => bytes[0] == 0xFF && bytes[1] == 0xD8,
                ".png" => bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47,
                ".gif" => (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46),
                ".bmp" => bytes[0] == 0x42 && bytes[1] == 0x4D,
                ".tiff" or ".tif" => (bytes[0] == 0x49 && bytes[1] == 0x49) || (bytes[0] == 0x4D && bytes[1] == 0x4D),
                ".webp" => bytes.Length >= 12 && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50,
                _ => true // For unknown extensions, assume valid
            };
        }

        public void Dispose()
        {
            _fileSemaphore?.Dispose();
        }
    }
}