using System;
using System.Collections.Concurrent;
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
        
        // Track active operations for status and cancellation
        private static readonly ConcurrentDictionary<Guid, MosaicOperationInfo> _activeOperations = new();
        private static readonly ConcurrentDictionary<Guid, byte[]> _previewCache = new();

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

        #region Status, Cancellation and Preview Methods

        /// <summary>
        /// Gets the status of a mosaic operation
        /// </summary>
        public virtual Task<MosaicStatus> GetMosaicStatusAsync(Guid mosaicId)
        {
            Logger.LogDebug("Getting status for mosaic operation: {MosaicId}", mosaicId);
            
            if (_activeOperations.TryGetValue(mosaicId, out var operation))
            {
                Logger.LogDebug("Found active operation {MosaicId} with status: {Status}", mosaicId, operation.Status);
                return Task.FromResult(operation.Status);
            }

            Logger.LogDebug("No active operation found for {MosaicId}, returning Unknown status", mosaicId);
            return Task.FromResult(MosaicStatus.Unknown);
        }

        /// <summary>
        /// Cancels a running mosaic operation
        /// </summary>
        public virtual Task<bool> CancelMosaicAsync(Guid mosaicId)
        {
            Logger.LogInformation("Attempting to cancel mosaic operation: {MosaicId}", mosaicId);
            
            if (_activeOperations.TryGetValue(mosaicId, out var operation))
            {
                try
                {
                    if (operation.CancellationTokenSource != null && !operation.CancellationTokenSource.Token.IsCancellationRequested)
                    {
                        operation.CancellationTokenSource.Cancel();
                        operation.Status = MosaicStatus.Cancelled;
                        operation.CompletedAt = DateTime.UtcNow;
                        operation.ErrorMessage = "Operation cancelled by user request";
                        
                        Logger.LogInformation("Successfully cancelled mosaic operation: {MosaicId}", mosaicId);
                        return Task.FromResult(true);
                    }
                    else
                    {
                        Logger.LogWarning("Mosaic operation {MosaicId} was already cancelled", mosaicId);
                        return Task.FromResult(true); // Already cancelled
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error cancelling mosaic operation: {MosaicId}", mosaicId);
                    return Task.FromResult(false);
                }
            }

            Logger.LogWarning("Cannot cancel mosaic operation {MosaicId} - operation not found", mosaicId);
            return Task.FromResult(false);
        }

        /// <summary>
        /// Gets a preview/thumbnail of the mosaic being created
        /// </summary>
        public virtual async Task<byte[]?> GetMosaicPreviewAsync(Guid mosaicId)
        {
            Logger.LogDebug("Getting preview for mosaic operation: {MosaicId}", mosaicId);

            // Check cache first
            if (_previewCache.TryGetValue(mosaicId, out var cachedPreview))
            {
                Logger.LogDebug("Found cached preview for {MosaicId} ({Size} bytes)", mosaicId, cachedPreview.Length);
                return cachedPreview;
            }

            // Check if operation exists and has output
            if (_activeOperations.TryGetValue(mosaicId, out var operation))
            {
                // If completed and has output path, generate preview
                if (operation.Status == MosaicStatus.Completed && !string.IsNullOrEmpty(operation.OutputPath))
                {
                    try
                    {
                        if (System.IO.File.Exists(operation.OutputPath))
                        {
                            var previewBytes = await ImageProcessingService.CreateThumbnailAsync(
                                operation.OutputPath, 400, 300, CancellationToken.None);
                            
                            // Cache for future requests
                            _previewCache.TryAdd(mosaicId, previewBytes);
                            
                            Logger.LogDebug("Generated preview for {MosaicId} ({Size} bytes)", mosaicId, previewBytes.Length);
                            return previewBytes;
                        }
                        else
                        {
                            Logger.LogWarning("Output file not found for mosaic {MosaicId}: {OutputPath}", mosaicId, operation.OutputPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error generating preview for mosaic {MosaicId}", mosaicId);
                    }
                }
                else
                {
                    Logger.LogDebug("Mosaic {MosaicId} is not completed or has no output path (Status: {Status})", mosaicId, operation.Status);
                }
            }
            else
            {
                Logger.LogDebug("No active operation found for {MosaicId}", mosaicId);
            }

            return null;
        }

        /// <summary>
        /// Validates a mosaic request (generic implementation)
        /// </summary>
        public virtual Task<ValidationResult> ValidateMosaicRequestAsync(object request)
        {
            var result = new ValidationResult { IsValid = true };
            
            if (request == null)
            {
                result.IsValid = false;
                result.AddError("Request cannot be null");
                return Task.FromResult(result);
            }

            // Basic validation - specific services can override for more detailed validation
            Logger.LogDebug("Validating mosaic request of type: {RequestType}", request.GetType().Name);
            
            return Task.FromResult(result);
        }

        /// <summary>
        /// Registers an operation for tracking
        /// </summary>
        protected void RegisterOperation(Guid mosaicId, CancellationTokenSource cancellationTokenSource, string? outputPath = null)
        {
            var operation = new MosaicOperationInfo
            {
                OperationId = mosaicId,
                Status = MosaicStatus.Initializing,
                StartedAt = DateTime.UtcNow,
                CancellationTokenSource = cancellationTokenSource,
                OutputPath = outputPath
            };

            _activeOperations.TryAdd(mosaicId, operation);
            Logger.LogDebug("Registered operation for tracking: {MosaicId}", mosaicId);
        }

        /// <summary>
        /// Updates the status of an operation
        /// </summary>
        protected void UpdateOperationStatus(Guid mosaicId, MosaicStatus status, string? currentStep = null, double? progressPercentage = null)
        {
            if (_activeOperations.TryGetValue(mosaicId, out var operation))
            {
                operation.Status = status;
                if (!string.IsNullOrEmpty(currentStep))
                    operation.CurrentStep = currentStep;
                if (progressPercentage.HasValue)
                    operation.ProgressPercentage = progressPercentage.Value;
                
                if (status == MosaicStatus.Completed || status == MosaicStatus.Failed || status == MosaicStatus.Cancelled)
                {
                    operation.CompletedAt = DateTime.UtcNow;
                }

                Logger.LogDebug("Updated operation {MosaicId} status to: {Status}", mosaicId, status);
            }
        }

        /// <summary>
        /// Unregisters an operation when completed
        /// </summary>
        protected void UnregisterOperation(Guid mosaicId)
        {
            if (_activeOperations.TryRemove(mosaicId, out var operation))
            {
                operation.CancellationTokenSource?.Dispose();
                Logger.LogDebug("Unregistered operation: {MosaicId}", mosaicId);
            }
            
            // Remove from preview cache as well
            _previewCache.TryRemove(mosaicId, out _);
        }

        #endregion
    }
}