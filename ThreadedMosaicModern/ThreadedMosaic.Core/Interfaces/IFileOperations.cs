using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadedMosaic.Core.Interfaces
{
    /// <summary>
    /// Interface for file operations that can be mocked for testing.
    /// Modernized version with async support and better error handling.
    /// Separates file system operations from business logic.
    /// </summary>
    public interface IFileOperations
    {
        /// <summary>
        /// Opens an image file for viewing
        /// </summary>
        /// <param name="imageLocation">Path to the image file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task OpenImageFileAsync(string imageLocation, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates if the file exists and is accessible
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>True if file is valid and accessible</returns>
        Task<bool> IsValidImageFileAsync(string filePath);

        /// <summary>
        /// Gets the file size in bytes
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>File size in bytes</returns>
        Task<long> GetFileSizeAsync(string filePath);

        /// <summary>
        /// Copies a file to a destination with progress reporting
        /// </summary>
        /// <param name="sourcePath">Source file path</param>
        /// <param name="destinationPath">Destination file path</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task CopyFileAsync(string sourcePath, string destinationPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a file safely
        /// </summary>
        /// <param name="filePath">Path to the file to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets available disk space in bytes for the specified path
        /// </summary>
        /// <param name="path">Path to check</param>
        /// <returns>Available disk space in bytes</returns>
        Task<long> GetAvailableDiskSpaceAsync(string path);

        /// <summary>
        /// Creates a temporary directory for processing
        /// </summary>
        /// <returns>Path to the created temporary directory</returns>
        Task<string> CreateTempDirectoryAsync();

        /// <summary>
        /// Cleans up temporary files and directories
        /// </summary>
        /// <param name="tempPath">Temporary path to clean up</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task CleanupTempFilesAsync(string tempPath, CancellationToken cancellationToken = default);
    }
}