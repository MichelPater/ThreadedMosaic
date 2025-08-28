using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThreadedMosaic.Core.DTOs;
using ThreadedMosaic.Core.Interfaces;
using ThreadedMosaic.Core.Models;

namespace ThreadedMosaic.Core.Services
{
    /// <summary>
    /// Color-based mosaic creation service
    /// Creates mosaics by matching colors directly
    /// </summary>
    public class ColorMosaicService : MosaicServiceBase, IMosaicService
    {
        public ColorMosaicService(
            IImageProcessingService imageProcessingService,
            IFileOperations fileOperations,
            ILogger<ColorMosaicService> logger)
            : base(imageProcessingService, fileOperations, logger)
        {
        }

        public async Task<MosaicResult> CreateColorMosaicAsync(
            ColorMosaicRequest request, 
            IProgressReporter? progressReporter = null, 
            CancellationToken cancellationToken = default)
        {
            return await CreateMosaicAsync(request, progressReporter, cancellationToken).ConfigureAwait(false);
        }

        public Task<MosaicResult> CreateHueMosaicAsync(
            HueMosaicRequest request, 
            IProgressReporter? progressReporter = null, 
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Use HueMosaicService for hue-based mosaics");
        }

        public Task<MosaicResult> CreatePhotoMosaicAsync(
            PhotoMosaicRequest request, 
            IProgressReporter? progressReporter = null, 
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Use PhotoMosaicService for photo-based mosaics");
        }

        public override async Task<MosaicResult> CreateMosaicAsync<TRequest>(
            TRequest request, 
            IProgressReporter? progressReporter = null, 
            CancellationToken cancellationToken = default)
        {
            if (request is not ColorMosaicRequest colorRequest)
                throw new ArgumentException("Request must be ColorMosaicRequest", nameof(request));

            var mosaicId = Guid.NewGuid();
            var result = CreateMosaicResult(mosaicId, colorRequest.OutputPath);

            try
            {
                result.Status = MosaicStatus.Initializing;
                
                // Validate request
                var validation = await ValidateRequestAsync(colorRequest).ConfigureAwait(false);
                if (!validation.IsValid)
                {
                    result.Status = MosaicStatus.Failed;
                    result.ErrorMessage = string.Join("; ", validation.Errors);
                    return result;
                }

                if (progressReporter != null)
                {
                    await progressReporter.UpdateStatusAsync("Initializing color mosaic creation...", cancellationToken).ConfigureAwait(false);
                }

                // Load master image
                result.Status = MosaicStatus.LoadingImages;
                Logger.LogInformation("Loading master image: {MasterImagePath}", colorRequest.MasterImagePath);
                
                var masterImage = await ImageProcessingService.LoadImageAsync(colorRequest.MasterImagePath, cancellationToken).ConfigureAwait(false);
                
                // Load seed images
                var seedImages = await LoadSeedImagesAsync(colorRequest.SeedImagesDirectory, progressReporter, cancellationToken).ConfigureAwait(false);
                
                if (!seedImages.Any())
                {
                    result.Status = MosaicStatus.Failed;
                    result.ErrorMessage = "No valid seed images found";
                    return result;
                }

                // Create tile color grid
                result.Status = MosaicStatus.AnalyzingImages;
                var colorGrid = await CreateTileColorGridAsync(masterImage, colorRequest.PixelSize, progressReporter, cancellationToken).ConfigureAwait(false);

                // Create mosaic
                result.Status = MosaicStatus.CreatingMosaic;
                var mosaicImage = await CreateColorMosaicImageAsync(colorGrid, seedImages, colorRequest, progressReporter, cancellationToken).ConfigureAwait(false);

                // Save result
                result.Status = MosaicStatus.Saving;
                var outputPath = await SaveMosaicAsync(mosaicImage, colorRequest.OutputPath, colorRequest.OutputFormat, colorRequest.Quality, progressReporter, cancellationToken).ConfigureAwait(false);

                result.Status = MosaicStatus.Completed;
                result.CompletedAt = DateTime.UtcNow;
                result.OutputPath = outputPath;
                result.ProcessingTime = result.CompletedAt - result.CreatedAt;

                UpdateStatistics(result, seedImages.ToList(), colorGrid);

                Logger.LogInformation("Color mosaic creation completed: {OutputPath}", outputPath);
                return result;
            }
            catch (OperationCanceledException)
            {
                result.Status = MosaicStatus.Cancelled;
                result.CompletedAt = DateTime.UtcNow;
                Logger.LogInformation("Color mosaic creation cancelled");
                return result;
            }
            catch (Exception ex)
            {
                HandleProcessingException(result, ex, "color mosaic creation");
                return result;
            }
        }

        private async Task<LoadedImage> CreateColorMosaicImageAsync(
            ColorInfo[,] colorGrid,
            IEnumerable<LoadedImage> seedImages,
            ColorMosaicRequest request,
            IProgressReporter? progressReporter,
            CancellationToken cancellationToken)
        {
            var seedImagesList = seedImages.ToList();
            var tilesWidth = colorGrid.GetLength(0);
            var tilesHeight = colorGrid.GetLength(1);
            var totalTiles = tilesWidth * tilesHeight;

            if (progressReporter != null)
            {
                await progressReporter.SetMaximumAsync(totalTiles, cancellationToken).ConfigureAwait(false);
            }

            Logger.LogInformation("Creating color mosaic: {Width}x{Height} tiles", tilesWidth, tilesHeight);

            // Create mosaic metadata
            var mosaicTiles = new MosaicTileMetadata[tilesWidth, tilesHeight];
            var processedTiles = 0;

            // Process tiles with color matching
            for (int x = 0; x < tilesWidth; x++)
            {
                for (int y = 0; y < tilesHeight; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var targetColor = colorGrid[x, y];
                    var bestMatch = await FindBestColorMatchAsync(targetColor, seedImagesList, request.ColorTolerance, cancellationToken).ConfigureAwait(false);

                    mosaicTiles[x, y] = new MosaicTileMetadata
                    {
                        XCoordinate = x * request.PixelSize,
                        YCoordinate = y * request.PixelSize,
                        TargetColor = targetColor,
                        SelectedImagePath = bestMatch.FilePath,
                        TilePixelSize = new TileSize(request.PixelSize, request.PixelSize),
                        ColorDistance = targetColor.GetDistanceTo(bestMatch.AverageColor),
                        ProcessedAt = DateTime.UtcNow
                    };

                    // Apply transparency overlay if specified
                    if (request.TransparencyIntensity > 0)
                    {
                        mosaicTiles[x, y].TransparencyOverlayColor = CalculateTransparencyOverlay(targetColor, request.TransparencyIntensity);
                    }

                    processedTiles++;
                    if (progressReporter != null && processedTiles % 50 == 0) // Update every 50 tiles
                    {
                        await progressReporter.ReportProgressAsync(
                            processedTiles, 
                            totalTiles, 
                            $"Matching colors... {processedTiles}/{totalTiles}", 
                            cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            // For now, create a simple colored image (actual image composition would require more complex image processing)
            var resultImage = new LoadedImage
            {
                Width = tilesWidth * request.PixelSize,
                Height = tilesHeight * request.PixelSize,
                Format = request.OutputFormat,
                FilePath = request.OutputPath,
                LoadedAt = DateTime.UtcNow
            };

            Logger.LogInformation("Color mosaic image created: {Width}x{Height}", resultImage.Width, resultImage.Height);
            return resultImage;
        }

        private async Task<LoadedImage> FindBestColorMatchAsync(
            ColorInfo targetColor,
            List<LoadedImage> seedImages,
            int tolerance,
            CancellationToken cancellationToken)
        {
            LoadedImage bestMatch = seedImages[0];
            var bestDistance = double.MaxValue;

            foreach (var seedImage in seedImages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var distance = targetColor.GetDistanceTo(seedImage.AverageColor);
                
                // Apply tolerance check
                if (distance <= tolerance && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestMatch = seedImage;
                }
            }

            return await Task.FromResult(bestMatch).ConfigureAwait(false);
        }

        private ColorInfo CalculateTransparencyOverlay(ColorInfo baseColor, int intensityPercentage)
        {
            var intensity = intensityPercentage / 100.0;
            return new ColorInfo(
                (byte)(baseColor.R * intensity),
                (byte)(baseColor.G * intensity),
                (byte)(baseColor.B * intensity),
                (byte)(255 * intensity)
            );
        }

        public Task<MosaicStatus> GetMosaicStatusAsync(Guid mosaicId)
        {
            // TODO: Implement status tracking with a status store
            return Task.FromResult(MosaicStatus.Unknown);
        }

        public Task<bool> CancelMosaicAsync(Guid mosaicId)
        {
            // TODO: Implement cancellation tracking
            return Task.FromResult(false);
        }

        public Task<byte[]?> GetMosaicPreviewAsync(Guid mosaicId)
        {
            // TODO: Implement preview generation
            return Task.FromResult<byte[]?>(null);
        }

        public async Task<ValidationResult> ValidateMosaicRequestAsync(object request)
        {
            if (request is ColorMosaicRequest colorRequest)
            {
                return await ValidateRequestAsync(colorRequest).ConfigureAwait(false);
            }
            
            return ValidationResult.Failure("Invalid request type for ColorMosaicService");
        }
    }

}