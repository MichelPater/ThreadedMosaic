using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThreadedMosaic.Core.Models;
using ThreadedMosaic.Core.Interfaces;
using ThreadedMosaic.Core.DTOs;

namespace ThreadedMosaic.Core.Services
{
    /// <summary>
    /// Modernized base class for mosaic creation services
    /// Replaces the legacy Mosaic class with async/await patterns and better resource management
    /// </summary>
    public abstract class MosaicServiceBase
    {
        protected readonly IImageProcessingService ImageProcessingService;
        protected readonly IFileOperations FileOperations;
        protected readonly ILogger Logger;

        protected MosaicServiceBase(
            IImageProcessingService imageProcessingService,
            IFileOperations fileOperations,
            ILogger logger)
        {
            ImageProcessingService = imageProcessingService ?? throw new ArgumentNullException(nameof(imageProcessingService));
            FileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a mosaic with the specified request parameters
        /// </summary>
        public abstract Task<MosaicResult> CreateMosaicAsync<TRequest>(
            TRequest request, 
            IProgressReporter? progressReporter = null,
            CancellationToken cancellationToken = default) 
            where TRequest : MosaicRequestBase;

        /// <summary>
        /// Validates the mosaic request parameters
        /// </summary>
        protected virtual async Task<ValidationResult> ValidateRequestAsync<TRequest>(TRequest request) 
            where TRequest : MosaicRequestBase
        {
            var result = new ValidationResult { IsValid = true };

            // Validate master image
            if (string.IsNullOrWhiteSpace(request.MasterImagePath))
            {
                result.AddError("Master image path is required");
            }
            else if (!await FileOperations.IsValidImageFileAsync(request.MasterImagePath))
            {
                result.AddError("Master image file is not valid or accessible");
            }

            // Validate seed images directory
            if (string.IsNullOrWhiteSpace(request.SeedImagesDirectory))
            {
                result.AddError("Seed images directory is required");
            }

            // Validate output path
            if (string.IsNullOrWhiteSpace(request.OutputPath))
            {
                result.AddError("Output path is required");
            }

            // Validate pixel size
            if (request.PixelSize < 1 || request.PixelSize > 1000)
            {
                result.AddError("Pixel size must be between 1 and 1000");
            }

            // Validate quality
            if (request.Quality < 1 || request.Quality > 100)
            {
                result.AddError("Quality must be between 1 and 100");
            }

            // Check available disk space
            try
            {
                var availableSpace = await FileOperations.GetAvailableDiskSpaceAsync(request.OutputPath);
                if (availableSpace < 100 * 1024 * 1024) // 100MB minimum
                {
                    result.AddWarning("Low disk space available for output");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not check available disk space");
                result.AddWarning("Could not verify available disk space");
            }

            result.IsValid = result.Errors.Count == 0;
            return result;
        }

        /// <summary>
        /// Loads and analyzes seed images from the specified directory
        /// </summary>
        protected async Task<List<LoadedImage>> LoadSeedImagesAsync(
            string seedImagesDirectory,
            IProgressReporter? progressReporter = null,
            CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Loading seed images from directory: {Directory}", seedImagesDirectory);

            if (progressReporter != null)
            {
                await progressReporter.UpdateStatusAsync("Loading seed images...", cancellationToken).ConfigureAwait(false);
            }

            var seedImages = await ImageProcessingService.LoadImagesFromDirectoryAsync(
                seedImagesDirectory, 
                progressReporter, 
                cancellationToken).ConfigureAwait(false);

            var seedImagesList = seedImages.ToList();
            Logger.LogInformation("Loaded {Count} seed images", seedImagesList.Count);

            return seedImagesList;
        }

        /// <summary>
        /// Creates tile color grid from the master image
        /// </summary>
        protected async Task<ColorInfo[,]> CreateTileColorGridAsync(
            LoadedImage masterImage,
            int pixelSize,
            IProgressReporter? progressReporter = null,
            CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Creating tile color grid with pixel size: {PixelSize}", pixelSize);

            if (progressReporter != null)
            {
                await progressReporter.UpdateStatusAsync("Analyzing master image colors...", cancellationToken).ConfigureAwait(false);
            }

            // Calculate grid dimensions
            var tilesWidth = (int)Math.Ceiling((double)masterImage.Width / pixelSize);
            var tilesHeight = (int)Math.Ceiling((double)masterImage.Height / pixelSize);

            var colorGrid = new ColorInfo[tilesWidth, tilesHeight];
            var totalTiles = tilesWidth * tilesHeight;

            if (progressReporter != null)
            {
                await progressReporter.SetMaximumAsync(totalTiles, cancellationToken).ConfigureAwait(false);
            }

            var processedTiles = 0;

            // Process tiles in parallel for better performance
            var tasks = new List<Task>();
            var semaphore = new SemaphoreSlim(Environment.ProcessorCount);

            for (int x = 0; x < tilesWidth; x++)
            {
                for (int y = 0; y < tilesHeight; y++)
                {
                    var localX = x;
                    var localY = y;

                    tasks.Add(Task.Run(async () =>
                    {
                        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                        try
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            // Calculate tile bounds
                            var tileX = localX * pixelSize;
                            var tileY = localY * pixelSize;
                            var tileWidth = Math.Min(pixelSize, masterImage.Width - tileX);
                            var tileHeight = Math.Min(pixelSize, masterImage.Height - tileY);

                            // Extract and analyze tile color
                            var tileColor = await ExtractTileColorAsync(masterImage, tileX, tileY, tileWidth, tileHeight, cancellationToken).ConfigureAwait(false);
                            colorGrid[localX, localY] = tileColor;

                            // Update progress
                            var currentProgress = Interlocked.Increment(ref processedTiles);
                            if (progressReporter != null && currentProgress % 10 == 0) // Update every 10 tiles to reduce overhead
                            {
                                await progressReporter.ReportProgressAsync(
                                    currentProgress, 
                                    totalTiles, 
                                    $"Analyzing tile {currentProgress}/{totalTiles}", 
                                    cancellationToken).ConfigureAwait(false);
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, cancellationToken));
                }
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            Logger.LogInformation("Created {Width}x{Height} tile grid ({Total} tiles)", tilesWidth, tilesHeight, totalTiles);
            return colorGrid;
        }

        /// <summary>
        /// Extracts the average color from a specific tile region
        /// </summary>
        protected virtual async Task<ColorInfo> ExtractTileColorAsync(
            LoadedImage image,
            int x, 
            int y, 
            int width, 
            int height,
            CancellationToken cancellationToken = default)
        {
            // This is a placeholder implementation - actual implementation would depend on the image library used
            // For now, return a basic color calculation
            await Task.Delay(1, cancellationToken).ConfigureAwait(false); // Simulate async work

            // Calculate a simple average color based on position (this is a placeholder)
            var normalizedX = (double)x / image.Width;
            var normalizedY = (double)y / image.Height;

            var r = (byte)(normalizedX * 255);
            var g = (byte)(normalizedY * 255);
            var b = (byte)((normalizedX + normalizedY) / 2 * 255);

            return new ColorInfo(r, g, b);
        }

        /// <summary>
        /// Finds the best matching seed image for a target color
        /// </summary>
        protected virtual Task<LoadedImage> FindBestMatchAsync(
            ColorInfo targetColor,
            List<LoadedImage> seedImages,
            CancellationToken cancellationToken = default)
        {
            LoadedImage bestMatch = seedImages[0];
            var bestDistance = double.MaxValue;

            foreach (var seedImage in seedImages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var distance = targetColor.GetDistanceTo(seedImage.AverageColor);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestMatch = seedImage;
                }
            }

            return Task.FromResult(bestMatch);
        }

        /// <summary>
        /// Saves the completed mosaic with the specified quality settings
        /// </summary>
        protected async Task<string> SaveMosaicAsync(
            LoadedImage mosaicImage,
            string outputPath,
            ImageFormat format,
            int quality,
            IProgressReporter? progressReporter = null,
            CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Saving mosaic to: {OutputPath}", outputPath);

            if (progressReporter != null)
            {
                await progressReporter.UpdateStatusAsync("Saving mosaic image...", cancellationToken).ConfigureAwait(false);
            }

            await ImageProcessingService.SaveImageAsync(mosaicImage, outputPath, format, quality, cancellationToken).ConfigureAwait(false);

            // Verify the saved file
            if (await FileOperations.IsValidImageFileAsync(outputPath))
            {
                var fileSize = await FileOperations.GetFileSizeAsync(outputPath);
                Logger.LogInformation("Mosaic saved successfully. File size: {FileSize} bytes", fileSize);
                return outputPath;
            }
            else
            {
                throw new InvalidOperationException("Failed to save mosaic image properly");
            }
        }

        /// <summary>
        /// Creates a processing session with tracking
        /// </summary>
        protected MosaicResult CreateMosaicResult(Guid mosaicId, string outputPath)
        {
            return new MosaicResult
            {
                MosaicId = mosaicId,
                OutputPath = outputPath,
                Status = MosaicStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Updates processing statistics
        /// </summary>
        protected void UpdateStatistics(MosaicResult result, List<LoadedImage> seedImages, ColorInfo[,] colorGrid)
        {
            if (result.Statistics == null)
            {
                result.Statistics = new MosaicStatistics();
            }

            result.Statistics.TotalSeedImages = seedImages.Count;
            result.Statistics.ValidSeedImages = seedImages.Count(img => !img.IsProcessed || string.IsNullOrEmpty(img.ProcessingError));
            result.TotalTiles = colorGrid.GetLength(0) * colorGrid.GetLength(1);
        }

        /// <summary>
        /// Handles exceptions during processing
        /// </summary>
        protected void HandleProcessingException(MosaicResult result, Exception ex, string context)
        {
            Logger.LogError(ex, "Error during {Context}", context);
            result.Status = MosaicStatus.Failed;
            result.ErrorMessage = $"Error in {context}: {ex.Message}";
            result.CompletedAt = DateTime.UtcNow;
        }
    }
}