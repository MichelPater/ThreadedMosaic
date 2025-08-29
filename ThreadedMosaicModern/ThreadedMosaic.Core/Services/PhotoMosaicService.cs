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
    /// Photo-based mosaic creation service
    /// Creates mosaics using actual photos matched to color regions with optional repetition avoidance
    /// </summary>
    public class PhotoMosaicService : MosaicServiceBase, IMosaicService, IPhotoMosaicService
    {
        private readonly Dictionary<string, int> _imageUsageCount = new();
        private readonly HashSet<string> _recentlyUsedImages = new();

        public PhotoMosaicService(
            IImageProcessingService imageProcessingService,
            IFileOperations fileOperations,
            ILogger<PhotoMosaicService> logger)
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

        public Task<MosaicResult> CreateHueMosaicAsync(
            HueMosaicRequest request, 
            IProgressReporter? progressReporter = null, 
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Use HueMosaicService for hue-based mosaics");
        }

        public async Task<MosaicResult> CreatePhotoMosaicAsync(
            PhotoMosaicRequest request, 
            IProgressReporter? progressReporter = null, 
            CancellationToken cancellationToken = default)
        {
            return await CreateMosaicAsync(request, progressReporter, cancellationToken).ConfigureAwait(false);
        }

        public override async Task<MosaicResult> CreateMosaicAsync<TRequest>(
            TRequest request, 
            IProgressReporter? progressReporter = null, 
            CancellationToken cancellationToken = default)
        {
            if (request is not PhotoMosaicRequest photoRequest)
                throw new ArgumentException("Request must be PhotoMosaicRequest", nameof(request));

            var mosaicId = Guid.NewGuid();
            var result = CreateMosaicResult(mosaicId, photoRequest.OutputPath);

            // Reset tracking collections for this session
            _imageUsageCount.Clear();
            _recentlyUsedImages.Clear();

            try
            {
                result.Status = MosaicStatus.Initializing;
                
                // Validate request
                var validation = await ValidateRequestAsync(photoRequest).ConfigureAwait(false);
                if (!validation.IsValid)
                {
                    result.Status = MosaicStatus.Failed;
                    result.ErrorMessage = string.Join("; ", validation.Errors);
                    return result;
                }

                if (progressReporter != null)
                {
                    await progressReporter.UpdateStatusAsync("Initializing photo mosaic creation...", cancellationToken).ConfigureAwait(false);
                }

                // Load master image
                result.Status = MosaicStatus.LoadingImages;
                Logger.LogInformation("Loading master image: {MasterImagePath}", photoRequest.MasterImagePath);
                
                var masterImage = await ImageProcessingService.LoadImageAsync(photoRequest.MasterImagePath, cancellationToken).ConfigureAwait(false);
                
                // Load seed images
                var seedImages = await LoadSeedImagesAsync(photoRequest.SeedImagesDirectory, progressReporter, cancellationToken).ConfigureAwait(false);
                
                if (!seedImages.Any())
                {
                    result.Status = MosaicStatus.Failed;
                    result.ErrorMessage = "No valid seed images found";
                    return result;
                }

                // Initialize usage tracking
                foreach (var image in seedImages)
                {
                    _imageUsageCount[image.FilePath] = 0;
                }

                // Create tile color grid
                result.Status = MosaicStatus.AnalyzingImages;
                var colorGrid = await CreateTileColorGridAsync(masterImage, photoRequest.PixelSize, progressReporter, cancellationToken).ConfigureAwait(false);

                // Create mosaic
                result.Status = MosaicStatus.CreatingMosaic;
                var mosaicImage = await CreatePhotoMosaicImageAsync(colorGrid, seedImages, photoRequest, progressReporter, cancellationToken).ConfigureAwait(false);

                // Save result
                result.Status = MosaicStatus.Saving;
                var outputPath = await SaveMosaicAsync(mosaicImage, photoRequest.OutputPath, photoRequest.OutputFormat, photoRequest.Quality, progressReporter, cancellationToken).ConfigureAwait(false);

                result.Status = MosaicStatus.Completed;
                result.CompletedAt = DateTime.UtcNow;
                result.OutputPath = outputPath;
                result.ProcessingTime = result.CompletedAt - result.CreatedAt;

                UpdateStatistics(result, seedImages.ToList(), colorGrid);
                
                // Update statistics with usage information
                if (result.Statistics != null)
                {
                    result.Statistics.ImageUsageCount = new Dictionary<string, int>(_imageUsageCount);
                    var mostUsed = _imageUsageCount.OrderByDescending(kvp => kvp.Value).FirstOrDefault();
                    result.Statistics.MostUsedTilePath = mostUsed.Key;
                    result.Statistics.MostUsedTileCount = mostUsed.Value;
                    result.Statistics.UniqueTilesUsed = _imageUsageCount.Count(kvp => kvp.Value > 0);
                }

                Logger.LogInformation("Photo mosaic creation completed: {OutputPath}", outputPath);
                return result;
            }
            catch (OperationCanceledException)
            {
                result.Status = MosaicStatus.Cancelled;
                result.CompletedAt = DateTime.UtcNow;
                Logger.LogInformation("Photo mosaic creation cancelled");
                return result;
            }
            catch (Exception ex)
            {
                HandleProcessingException(result, ex, "photo mosaic creation");
                return result;
            }
        }

        private async Task<LoadedImage> CreatePhotoMosaicImageAsync(
            ColorInfo[,] colorGrid,
            IEnumerable<LoadedImage> seedImages,
            PhotoMosaicRequest request,
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

            Logger.LogInformation("Creating photo mosaic: {Width}x{Height} tiles", tilesWidth, tilesHeight);

            // Create mosaic metadata
            var mosaicTiles = new MosaicTileMetadata[tilesWidth, tilesHeight];
            var processedTiles = 0;

            // Process tiles with photo matching and repetition avoidance
            for (int x = 0; x < tilesWidth; x++)
            {
                for (int y = 0; y < tilesHeight; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var targetColor = colorGrid[x, y];
                    var bestMatch = await FindBestPhotoMatchAsync(
                        targetColor, 
                        seedImagesList, 
                        request.SimilarityThreshold,
                        request.AvoidImageRepetition,
                        request.MaxImageReuse,
                        request.UseEdgeDetection,
                        cancellationToken).ConfigureAwait(false);

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

                    // Apply subtle color overlay (alpha = 210 as in original)
                    mosaicTiles[x, y].TransparencyOverlayColor = CreatePhotoTransparencyOverlay(targetColor);

                    // Update usage tracking
                    _imageUsageCount[bestMatch.FilePath]++;

                    // Update recently used tracking (avoid immediate reuse)
                    UpdateRecentlyUsedImages(bestMatch.FilePath);

                    processedTiles++;
                    if (progressReporter != null && processedTiles % 25 == 0) // Update every 25 tiles
                    {
                        await progressReporter.ReportProgressAsync(
                            processedTiles, 
                            totalTiles, 
                            $"Placing photos... {processedTiles}/{totalTiles}", 
                            cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            // Create result image (actual composition would require complex image blending)
            var resultImage = new LoadedImage
            {
                Width = tilesWidth * request.PixelSize,
                Height = tilesHeight * request.PixelSize,
                Format = request.OutputFormat,
                FilePath = request.OutputPath,
                LoadedAt = DateTime.UtcNow
            };

            Logger.LogInformation("Photo mosaic image created: {Width}x{Height}", resultImage.Width, resultImage.Height);
            return resultImage;
        }

        private async Task<LoadedImage> FindBestPhotoMatchAsync(
            ColorInfo targetColor,
            List<LoadedImage> seedImages,
            int similarityThreshold,
            bool avoidRepetition,
            int maxImageReuse,
            bool useEdgeDetection,
            CancellationToken cancellationToken)
        {
            var candidateImages = seedImages.AsEnumerable();

            // Filter by usage count if avoiding repetition
            if (avoidRepetition && maxImageReuse > 0)
            {
                candidateImages = candidateImages.Where(img => 
                    _imageUsageCount.GetValueOrDefault(img.FilePath, 0) < maxImageReuse);
            }

            // Filter out recently used images to avoid immediate repetition
            if (avoidRepetition)
            {
                candidateImages = candidateImages.Where(img => !_recentlyUsedImages.Contains(img.FilePath));
            }

            var candidateList = candidateImages.ToList();
            
            // If we filtered out everything, fallback to all images
            if (!candidateList.Any())
            {
                candidateList = seedImages;
            }

            LoadedImage bestMatch = candidateList[0];
            var bestScore = double.MaxValue;

            foreach (var seedImage in candidateList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var colorDistance = targetColor.GetDistanceTo(seedImage.AverageColor);
                var score = colorDistance;

                // Apply similarity threshold
                var similarityScore = (1.0 - colorDistance / 441.67) * 100; // 441.67 is max distance (sqrt(255²+255²+255²))
                
                if (similarityScore < similarityThreshold)
                    continue; // Skip images below similarity threshold

                // Bonus for less frequently used images
                if (avoidRepetition)
                {
                    var usageCount = _imageUsageCount.GetValueOrDefault(seedImage.FilePath, 0);
                    score += usageCount * 10; // Penalty for overused images
                }

                // TODO: Implement edge detection scoring if useEdgeDetection is true
                if (useEdgeDetection)
                {
                    // Placeholder for edge detection logic
                    // This would compare edge patterns between target region and candidate image
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    bestMatch = seedImage;
                }
            }

            return await Task.FromResult(bestMatch).ConfigureAwait(false);
        }

        private ColorInfo CreatePhotoTransparencyOverlay(ColorInfo targetColor)
        {
            // Create a semi-transparent overlay to adjust photo color toward target (alpha = 210)
            const byte alpha = 210;
            return new ColorInfo(targetColor.R, targetColor.G, targetColor.B, alpha);
        }

        private void UpdateRecentlyUsedImages(string imagePath)
        {
            _recentlyUsedImages.Add(imagePath);
            
            // Keep only last 20 images to avoid immediate repetition but allow eventual reuse
            if (_recentlyUsedImages.Count > 20)
            {
                var oldest = _recentlyUsedImages.First();
                _recentlyUsedImages.Remove(oldest);
            }
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
            if (request is PhotoMosaicRequest photoRequest)
            {
                return await ValidateRequestAsync(photoRequest).ConfigureAwait(false);
            }
            
            return ValidationResult.Failure("Invalid request type for PhotoMosaicService");
        }
    }
}