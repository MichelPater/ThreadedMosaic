using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThreadedMosaic.Core.Models;
using ThreadedMosaic.Core.DTOs;

namespace ThreadedMosaic.Core.Interfaces
{
    /// <summary>
    /// Interface for image processing operations
    /// </summary>
    public interface IImageProcessingService
    {
        /// <summary>
        /// Loads an image from file path
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Loaded image data</returns>
        Task<LoadedImage> LoadImageAsync(string imagePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads multiple images from a directory
        /// </summary>
        /// <param name="directoryPath">Directory containing images</param>
        /// <param name="progressReporter">Progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of loaded images</returns>
        Task<IEnumerable<LoadedImage>> LoadImagesFromDirectoryAsync(string directoryPath, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resizes an image to the specified dimensions
        /// </summary>
        /// <param name="image">Image to resize</param>
        /// <param name="width">Target width</param>
        /// <param name="height">Target height</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Resized image</returns>
        Task<LoadedImage> ResizeImageAsync(LoadedImage image, int width, int height, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a thumbnail of the specified image
        /// </summary>
        /// <param name="imagePath">Path to the source image</param>
        /// <param name="maxWidth">Maximum width of thumbnail</param>
        /// <param name="maxHeight">Maximum height of thumbnail</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Thumbnail image data</returns>
        Task<byte[]> CreateThumbnailAsync(string imagePath, int maxWidth, int maxHeight, CancellationToken cancellationToken = default);

        /// <summary>
        /// Analyzes an image to extract color and metadata information
        /// </summary>
        /// <param name="image">Image to analyze</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Image analysis result</returns>
        Task<ImageAnalysis> AnalyzeImageAsync(LoadedImage image, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves an image to the specified path with quality settings
        /// </summary>
        /// <param name="image">Image to save</param>
        /// <param name="outputPath">Output file path</param>
        /// <param name="format">Image format</param>
        /// <param name="quality">Quality setting (1-100 for JPEG)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SaveImageAsync(LoadedImage image, string outputPath, ImageFormat format, int quality = 95, CancellationToken cancellationToken = default);

        /// <summary>
        /// Converts an image between different formats
        /// </summary>
        /// <param name="inputPath">Input image path</param>
        /// <param name="outputPath">Output image path</param>
        /// <param name="targetFormat">Target image format</param>
        /// <param name="quality">Quality setting</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task ConvertImageFormatAsync(string inputPath, string outputPath, ImageFormat targetFormat, int quality = 95, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates if a file is a supported image format
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>True if the file is a supported image format</returns>
        Task<bool> IsValidImageFormatAsync(string filePath);

        /// <summary>
        /// Gets the dimensions of an image without fully loading it
        /// </summary>
        /// <param name="imagePath">Path to the image</param>
        /// <returns>Image dimensions</returns>
        Task<(int Width, int Height)> GetImageDimensionsAsync(string imagePath);
    }
}