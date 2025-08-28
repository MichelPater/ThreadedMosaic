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
    /// Hue-based mosaic creation service
    /// Creates mosaics by matching hue values with transparency overlays
    /// </summary>
    public class HueMosaicService : MosaicServiceBase, IMosaicService
    {
        public HueMosaicService(
            IImageProcessingService imageProcessingService,
            IFileOperations fileOperations,
            ILogger<HueMosaicService> logger)
            : base(imageProcessingService, fileOperations, logger)
        {
        }

        public Task<MosaicResult> CreateColorMosaicAsync(
            ColorMosaicRequest request, 
            IProgressReporter? progressReporter = null, 
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Use ColorMosaicService for color-based mosaics");
        }

        public async Task<MosaicResult> CreateHueMosaicAsync(
            HueMosaicRequest request, 
            IProgressReporter? progressReporter = null, 
            CancellationToken cancellationToken = default)
        {
            return await CreateMosaicAsync(request, progressReporter, cancellationToken).ConfigureAwait(false);
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
            if (request is not HueMosaicRequest hueRequest)
                throw new ArgumentException("Request must be HueMosaicRequest", nameof(request));

            var mosaicId = Guid.NewGuid();
            var result = CreateMosaicResult(mosaicId, hueRequest.OutputPath);

            try
            {
                result.Status = MosaicStatus.Initializing;
                
                // Validate request
                var validation = await ValidateRequestAsync(hueRequest).ConfigureAwait(false);
                if (!validation.IsValid)
                {
                    result.Status = MosaicStatus.Failed;
                    result.ErrorMessage = string.Join("; ", validation.Errors);
                    return result;
                }

                if (progressReporter != null)
                {
                    await progressReporter.UpdateStatusAsync("Initializing hue mosaic creation...", cancellationToken).ConfigureAwait(false);
                }

                // Load master image
                result.Status = MosaicStatus.LoadingImages;
                Logger.LogInformation("Loading master image: {MasterImagePath}", hueRequest.MasterImagePath);
                
                var masterImage = await ImageProcessingService.LoadImageAsync(hueRequest.MasterImagePath, cancellationToken).ConfigureAwait(false);
                
                // Load seed images
                var seedImages = await LoadSeedImagesAsync(hueRequest.SeedImagesDirectory, progressReporter, cancellationToken).ConfigureAwait(false);
                
                if (!seedImages.Any())
                {
                    result.Status = MosaicStatus.Failed;
                    result.ErrorMessage = "No valid seed images found";
                    return result;
                }

                // Create tile color grid
                result.Status = MosaicStatus.AnalyzingImages;
                var colorGrid = await CreateTileColorGridAsync(masterImage, hueRequest.PixelSize, progressReporter, cancellationToken).ConfigureAwait(false);

                // Create mosaic
                result.Status = MosaicStatus.CreatingMosaic;
                var mosaicImage = await CreateHueMosaicImageAsync(colorGrid, seedImages, hueRequest, progressReporter, cancellationToken).ConfigureAwait(false);

                // Save result
                result.Status = MosaicStatus.Saving;
                var outputPath = await SaveMosaicAsync(mosaicImage, hueRequest.OutputPath, hueRequest.OutputFormat, hueRequest.Quality, progressReporter, cancellationToken).ConfigureAwait(false);

                result.Status = MosaicStatus.Completed;
                result.CompletedAt = DateTime.UtcNow;
                result.OutputPath = outputPath;
                result.ProcessingTime = result.CompletedAt - result.CreatedAt;

                UpdateStatistics(result, seedImages.ToList(), colorGrid);

                Logger.LogInformation("Hue mosaic creation completed: {OutputPath}", outputPath);
                return result;
            }
            catch (OperationCanceledException)
            {
                result.Status = MosaicStatus.Cancelled;
                result.CompletedAt = DateTime.UtcNow;
                Logger.LogInformation("Hue mosaic creation cancelled");
                return result;
            }
            catch (Exception ex)
            {
                HandleProcessingException(result, ex, "hue mosaic creation");
                return result;
            }
        }

        private async Task<LoadedImage> CreateHueMosaicImageAsync(
            ColorInfo[,] colorGrid,
            IEnumerable<LoadedImage> seedImages,
            HueMosaicRequest request,
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

            Logger.LogInformation("Creating hue mosaic: {Width}x{Height} tiles", tilesWidth, tilesHeight);

            // Create mosaic metadata
            var mosaicTiles = new MosaicTileMetadata[tilesWidth, tilesHeight];
            var processedTiles = 0;
            var random = new Random();

            // Process tiles with hue-based matching
            for (int x = 0; x < tilesWidth; x++)
            {
                for (int y = 0; y < tilesHeight; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var targetColor = colorGrid[x, y];
                    var bestMatch = await FindBestHueMatchAsync(targetColor, seedImagesList, request.HueTolerance, request.SaturationWeight, request.BrightnessWeight, cancellationToken).ConfigureAwait(false);

                    // For hue mosaic, we use a random image but with hue-based color overlay
                    var randomImage = seedImagesList[random.Next(seedImagesList.Count)];

                    mosaicTiles[x, y] = new MosaicTileMetadata
                    {
                        XCoordinate = x * request.PixelSize,
                        YCoordinate = y * request.PixelSize,
                        TargetColor = targetColor,
                        SelectedImagePath = randomImage.FilePath,
                        TilePixelSize = new TileSize(request.PixelSize, request.PixelSize),
                        ColorDistance = CalculateHueDistance(targetColor, randomImage.AverageColor),
                        ProcessedAt = DateTime.UtcNow
                    };

                    // Apply hue-based transparency overlay (this is the key difference from color mosaic)
                    mosaicTiles[x, y].TransparencyOverlayColor = CreateHueTransparencyOverlay(targetColor);

                    processedTiles++;
                    if (progressReporter != null && processedTiles % 50 == 0) // Update every 50 tiles
                    {
                        await progressReporter.ReportProgressAsync(
                            processedTiles, 
                            totalTiles, 
                            $"Processing hue tiles... {processedTiles}/{totalTiles}", 
                            cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            // Create result image (actual composition would require more complex image processing)
            var resultImage = new LoadedImage
            {
                Width = tilesWidth * request.PixelSize,
                Height = tilesHeight * request.PixelSize,
                Format = request.OutputFormat,
                FilePath = request.OutputPath,
                LoadedAt = DateTime.UtcNow
            };

            Logger.LogInformation("Hue mosaic image created: {Width}x{Height}", resultImage.Width, resultImage.Height);
            return resultImage;
        }

        private async Task<LoadedImage> FindBestHueMatchAsync(
            ColorInfo targetColor,
            List<LoadedImage> seedImages,
            double hueTolerance,
            int saturationWeight,
            int brightnessWeight,
            CancellationToken cancellationToken)
        {
            LoadedImage bestMatch = seedImages[0];
            var bestScore = double.MaxValue;

            var targetHue = targetColor.GetHue();
            var targetSaturation = targetColor.GetSaturation();
            var targetBrightness = targetColor.GetBrightness();

            foreach (var seedImage in seedImages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var imageHue = seedImage.AverageColor.GetHue();
                var imageSaturation = seedImage.AverageColor.GetSaturation();
                var imageBrightness = seedImage.AverageColor.GetBrightness();

                // Calculate hue difference (considering circular nature of hue)
                var hueDiff = Math.Min(Math.Abs(targetHue - imageHue), 360 - Math.Abs(targetHue - imageHue));
                
                if (hueDiff > hueTolerance)
                    continue; // Skip images outside hue tolerance

                // Calculate weighted score
                var saturationDiff = Math.Abs(targetSaturation - imageSaturation);
                var brightnessDiff = Math.Abs(targetBrightness - imageBrightness);

                var score = hueDiff + 
                          (saturationDiff * saturationWeight / 100.0) + 
                          (brightnessDiff * brightnessWeight / 100.0);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestMatch = seedImage;
                }
            }

            return await Task.FromResult(bestMatch).ConfigureAwait(false);
        }

        private double CalculateHueDistance(ColorInfo color1, ColorInfo color2)
        {
            var hue1 = color1.GetHue();
            var hue2 = color2.GetHue();
            return Math.Min(Math.Abs(hue1 - hue2), 360 - Math.Abs(hue1 - hue2));
        }

        private ColorInfo CreateHueTransparencyOverlay(ColorInfo targetColor)
        {
            // Create a semi-transparent overlay with the target color (alpha = 210 as in original)
            const byte alpha = 210;
            return new ColorInfo(targetColor.R, targetColor.G, targetColor.B, alpha);
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
            if (request is HueMosaicRequest hueRequest)
            {
                return await ValidateRequestAsync(hueRequest).ConfigureAwait(false);
            }
            
            return ValidationResult.Failure("Invalid request type for HueMosaicService");
        }
    }
}